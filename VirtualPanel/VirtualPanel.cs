﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Metadata;

namespace VirtualPanel;

public class VirtualPanel : Panel, ILogicalScrollable, IChildIndexProvider
{
    #region ILogicalScrollable

    private Size _extent;
    private Vector _offset;
    private Size _viewport;
    private bool _canHorizontallyScroll;
    private bool _canVerticallyScroll;
    private bool _isLogicalScrollEnabled = true;
    private Size _scrollSize = new(1, 1);
    private Size _pageScrollSize = new(10, 10);
    private EventHandler? _scrollInvalidated;

    Size IScrollable.Extent => _extent;

    Vector IScrollable.Offset
    {
        get => _offset;
        set
        {
            _offset = value;
            CalculateSize(Bounds.Size);
            Materialize(_viewport, _offset, out _);
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
    private int _visibleCount = -1;
    private List<IControl> _controls = new();
    private List<int> _indexes = new();

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

    private void Materialize(Size viewport, Vector offset, out double topOffset)
    {
        var itemCount = Items?.Count ?? 0;
        var itemHeight = ItemHeight;

        _startIndex = (int)(offset.Y / itemHeight);
        _visibleCount = (int)(viewport.Height / itemHeight);

        if (_visibleCount < itemCount)
        {
            _visibleCount += 1;
        }

        topOffset = offset.Y % itemHeight;
        // topOffset = 0.0;

        /*
        Debug.WriteLine($"[Materialize] viewport: {viewport}" +
                        $", offset: {offset}" +
                        $", startIndex: {_startIndex}" +
                        $", visibleCount: {_visibleCount}" +
                        $", topOffset: {-topOffset}");
        //*/

        if (Items is null || Items.Count == 0 || ItemTemplate is null)
        {
            Children.Clear();
            _childIndexChanged?.Invoke(this, new ChildIndexChangedEventArgs());
            return;
        }

        {
            if (_controls.Count < _visibleCount)
            {
                var index = _startIndex + _controls.Count;
                if (index < Items.Count)
                {
                    for (var i = _controls.Count; i < _visibleCount; i++)
                    {
                        var param = Items[index];
                        var control = new ContentControl
                        {
                            Content = ItemTemplate.Build(param)
                        };
                        _controls.Add(control);
                        _indexes.Add(-1);
                        Children.Add(control);
                        Debug.WriteLine($"[Materialize.Create] index: {index}, param: {param}");
                        index++;
                    }
                }
            }
        }

        {
            var index = _startIndex;
            for (var i = 0; i < _controls.Count; i++)
            {
                var control = _controls[i];
                if (index >= Items.Count || i > _visibleCount)
                {
                    if (control.IsVisible)
                    {
                        control.IsVisible = false;
                        Debug.WriteLine($"[Materialize.Hide] index: {index}");
                    }
                    continue;
                }

                if (!control.IsVisible)
                {
                    control.IsVisible = true;
                    Debug.WriteLine($"[Materialize.Show] index: {index}");
                }

                var param = Items[index];
                control.DataContext = param;
                // Debug.WriteLine($"[Materialize.Update] index: {index}, param: {param}");
                _indexes[i] = index;
                index++;
            }  
        }

        _childIndexChanged?.Invoke(this, new ChildIndexChangedEventArgs());
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        availableSize = CalculateSize(availableSize);
            
        Materialize(_viewport, _offset, out _);

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

        Materialize(_viewport, _offset, out var topOffset);

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