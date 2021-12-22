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

namespace VirtualScrollDemo.Controls
{
    public class VirtualPanel : Panel, ILogicalScrollable, IChildIndexProvider
    {
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
        



        private int _startIndex = -1;
        private int _endIndex = -1;
        private List<IControl> _controls = new List<IControl>();

        private Size CalculateSize(Size size)
        {
            _viewport = size;

            var itemCount = Items.Count;
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
            var itemCount = Items.Count;
            var itemHeight = ItemHeight;

            var startIndex = (int)(offset.Y / itemHeight);
            var visibleCount = (int)(viewport.Height / itemHeight);
            var endIndex = startIndex + visibleCount - 1;

            topOffset = offset.Y % itemHeight;

            Debug.WriteLine($"viewport: {viewport}" +
                            $", extent: {extent}" +
                            $", offset: {offset}" +
                            $", startIndex: {startIndex}" +
                            $", endIndex: {endIndex}" +
                            $", visibleCount: {visibleCount}" +
                            $", topOffset: {-topOffset}" +
                            $", name: {name}");

            if (_controls.Count == 0)
            {
                var index = startIndex;
                for (var i = 0; i < visibleCount; i++)
                {
                    var param = Items[index];
                    var control = ItemTemplate.Build(param);
                    control.DataContext = param;
                    _controls.Add(control);
                    index++;
                }

                foreach (var control in _controls)
                {
                    Children.Add(control);
                }
            }
            else
            {
                var index = startIndex;
                for (var i = 0; i < visibleCount; i++)
                {
                    var param = Items[index];
                    var control = _controls[i];
                    control.DataContext = param;
                    index++;
                }  
            }


        }
        
        
        

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

        IControl ILogicalScrollable.GetControlInDirection(NavigationDirection direction, IControl @from)
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
                    Debug.WriteLine($"Measure: {size}");
                }
            }

            //return base.MeasureOverride(availableSize);
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
                    Debug.WriteLine($"Arrange: {rect}");
                }
            }

            //return base.ArrangeOverride(finalSize);
            return finalSize;
        }

        private void InvalidateScrollable()
        {
            if (this is not ILogicalScrollable scrollable)
            {
                return;
            }

            scrollable.RaiseScrollInvalidated(EventArgs.Empty);
        }
    }
}
