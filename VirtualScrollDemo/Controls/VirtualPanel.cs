using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Metadata;

namespace VirtualScrollDemo.Controls;

public class VirtualPanel : Panel, ILogicalScrollable, IChildIndexProvider
{
    #region ILogicalScrollable

    private Size _extent = new Size();
    private Vector _offset = new Vector();
    private Size _viewport = new Size();
    private bool _canHorizontallyScroll = false;
    private bool _canVerticallyScroll = false;
    private bool _isLogicalScrollEnabled = true;
    private Size _scrollSize = new Size(1, 1);
    private Size _pageScrollSize = new Size(10, 10);
    private EventHandler? _scrollInvalidated;

    Size IScrollable.Extent => _extent;

    Vector IScrollable.Offset
    {
        get => _offset;
        set
        {
            _offset = value;
            CalculateSize(Bounds.Size);
            Materialize(_viewport, _extent, _offset, out _);
            InvalidateScrollable();
            InvalidateMeasure();
        }
    }

    Size IScrollable.Viewport => _viewport;

    bool ILogicalScrollable.BringIntoView(IControl target, Rect targetRect)
    {
        return false;
    }

    IControl? ILogicalScrollable.GetControlInDirection(NavigationDirection direction, IControl @from)
    {
        return null;
    }

    void ILogicalScrollable.RaiseScrollInvalidated(EventArgs e)
    {
        _scrollInvalidated?.Invoke(this, e);
    }

    bool ILogicalScrollable.CanHorizontallyScroll
    {
        get => _canHorizontallyScroll;
        set => _canHorizontallyScroll = value;
    }

    bool ILogicalScrollable.CanVerticallyScroll
    {
        get => _canVerticallyScroll;
        set => _canVerticallyScroll = value;
    }

    bool ILogicalScrollable.IsLogicalScrollEnabled => _isLogicalScrollEnabled;

    Size ILogicalScrollable.ScrollSize => _scrollSize;

    Size ILogicalScrollable.PageScrollSize => _pageScrollSize;

    event EventHandler? ILogicalScrollable.ScrollInvalidated
    {
        add => _scrollInvalidated += value;
        remove => _scrollInvalidated -= value;
    }

    private void InvalidateScrollable()
    {
        if (this is not ILogicalScrollable scrollable)
        {
            return;
        }

        scrollable.RaiseScrollInvalidated(EventArgs.Empty);
    }

    #endregion

    #region IChildIndexProvider

    private EventHandler<ChildIndexChangedEventArgs>? _childIndexChanged;

    int IChildIndexProvider.GetChildIndex(ILogical child)
    {
        if (child is IControl control)
        {
            var indexOf = _controls.IndexOf(control);
            var index = _indexes[indexOf];
            // Debug.WriteLine($"[IChildIndexProvider.GetChildIndex] {indexOf} -> {index}");
            return index;
        }

        return -1;
    }

    bool IChildIndexProvider.TryGetTotalCount(out int count)
    {
        if (Items is { })
        {
            count = Items.Count;
            return true;
        }

        count = -1;
        return false;
    }

    event EventHandler<ChildIndexChangedEventArgs>? IChildIndexProvider.ChildIndexChanged
    {
        add => _childIndexChanged += value;
        remove => _childIndexChanged -= value;
    }

    #endregion

    #region Properties
                
    public static readonly StyledProperty<IList?> ItemsProperty = 
        AvaloniaProperty.Register<VirtualPanel, IList?>(nameof(Items));

    public static readonly StyledProperty<double> ItemHeightProperty = 
        AvaloniaProperty.Register<VirtualPanel, double>(nameof(ItemHeight), double.NaN);

    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty = 
        AvaloniaProperty.Register<VirtualPanel, IDataTemplate?>(nameof(ItemTemplate));

    public IList? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public double ItemHeight
    {
        get => GetValue(ItemHeightProperty);
        set => SetValue(ItemHeightProperty, value);
    }

    [Content]
    public IDataTemplate? ItemTemplate
    {
        get => GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    #endregion

    #region Layout

    private int _startIndex = -1;
    private int _endIndex = -1;
    private List<IControl> _controls = new List<IControl>();
    private List<int> _indexes = new List<int>();

    private Size CalculateSize(Size size)
    {
        _viewport = size;

        var itemCount = Items?.Count ?? 0;
        var itemHeight = ItemHeight;
        var height = itemCount * itemHeight;

        size = size.WithHeight(height);

        _extent = size;

        _scrollSize = new Size(16, 16);
        _pageScrollSize = new Size(_viewport.Width, _viewport.Height);

        return size;
    }

    private void Materialize(Size viewport, Size extent, Vector offset, out double topOffset, [CallerMemberName] string name = default)
    {
        var itemCount = Items?.Count ?? 0;
        var itemHeight = ItemHeight;

        var startIndex = (int)(offset.Y / itemHeight);
        var visibleCount = (int)(viewport.Height / itemHeight);

        if (visibleCount < itemCount)
        {
            visibleCount += 1;
        }

        var endIndex = startIndex + visibleCount - 2;

        topOffset = offset.Y % itemHeight;

        /*
        Debug.WriteLine($"[Materialize] viewport: {viewport}" +
                        $", extent: {extent}" +
                        $", offset: {offset}" +
                        $", startIndex: {startIndex}" +
                        $", endIndex: {endIndex}" +
                        $", visibleCount: {visibleCount}" +
                        $", topOffset: {-topOffset}" +
                        $", name: {name}");
        //*/

        if (Items is null || Items.Count == 0 || ItemTemplate is null)
        {
            Children.Clear();
            _childIndexChanged?.Invoke(this, new ChildIndexChangedEventArgs());
            return;
        }

        {
            if (_controls.Count < visibleCount)
            {
                var index = startIndex + _controls.Count;
                for (var i = _controls.Count; i < visibleCount; i++)
                {
                    var param = Items[index];
                    var control = ItemTemplate.Build(param);
                    _controls.Add(control);
                    _indexes.Add(-1);
                    Children.Add(control);
                    Debug.WriteLine($"[Materialize.Create] index: {index}, param: {param}");
                    index++;
                }
            }
        }

        {
            var index = startIndex;
            for (var i = 0; i < visibleCount; i++)
            {
                var control = _controls[i];
                if (index >= Items.Count)
                {
                    control.IsVisible = false;
                    continue;
                }

                if (!control.IsVisible)
                {
                    control.IsVisible = true;
                }

                var param = Items[index];
                control.DataContext = param;
                Debug.WriteLine($"[Materialize.Update] index: {index}, param: {param}");
                _indexes[i] = index;
                index++;
            }  
        }

        _childIndexChanged?.Invoke(this, new ChildIndexChangedEventArgs());
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        availableSize = CalculateSize(availableSize);
            
        Materialize(_viewport, _extent, _offset, out _);

        if (_controls.Count > 0)
        {
            foreach (var control in _controls)
            {
                var size = new Size(_viewport.Width, ItemHeight);
                control.Measure(size);
                // Debug.WriteLine($"[MeasureOverride.Measure] {size}");
            }
        }

        // return base.MeasureOverride(availableSize);
        return availableSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        finalSize = CalculateSize(finalSize);

        Materialize(_viewport, _extent, _offset, out var topOffset);

        InvalidateScrollable();

        if (_controls.Count > 0)
        {
            var y = topOffset == 0.0 ? 0.0 : -topOffset;

            foreach (var control in _controls)
            {
                var rect = new Rect(new Point(0, y), new Size(_viewport.Width, ItemHeight));
                control.Arrange(rect);
                y += ItemHeight;
                // Debug.WriteLine($"[ArrangeOverride.Arrange] {rect}");
            }
        }

        // return base.ArrangeOverride(finalSize);
        return finalSize;
    }

    #endregion
}
