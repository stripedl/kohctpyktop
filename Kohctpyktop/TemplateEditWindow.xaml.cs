using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kohctpyktop.ViewModels;

namespace Kohctpyktop
{
    public partial class TemplateEditWindow : Window
    {
        private enum ResizeType { NW, SE, NE, SW, W, E, N, S }
        
        public TemplateEditViewModel ViewModel { get; }
        
        private Point _prevPoint;
        private (int X, int Y) _prevPosition;
        private (int Width, int Height) _prevSize;
        private ICanvasObject _captured;
        private ResizeType _resizeType;
        
        public TemplateEditWindow(TemplateEditViewModel viewModel)
        {
            DataContext = ViewModel = viewModel;
            InitializeComponent();
        }

        private void CanvasItemMouseDown(object sender, MouseButtonEventArgs e)
        {
            var el = (FrameworkElement) sender;

            if (!(el.DataContext is ICanvasObject item))
                return;

            _captured = item;
            _prevPoint = e.GetPosition(ItemsCanvas);
            _prevPosition = (item.X, item.Y);
            
            Mouse.Capture(el);
        }

        private void CanvasItemMouseUp(object sender, MouseButtonEventArgs e)
        {
            var el = (FrameworkElement) sender;
            if (!ReferenceEquals(Mouse.Captured, el)) return;
            
            Mouse.Capture(null);
           
            MoveItem(e.GetPosition(ItemsCanvas));
        }

        private void CanvasItemMouseMove(object sender, MouseEventArgs e)
        {
            var el = (FrameworkElement) sender;
            if (Mouse.Captured == null) return;
            if (!ReferenceEquals(Mouse.Captured, el)) return;
            
            MoveItem(e.GetPosition(ItemsCanvas));
        }

        private void MoveItem(Point currentPos)
        {
            var diff = currentPos - _prevPoint;
            
            var diffX = (int) Math.Round(diff.X / 13);
            var diffY = (int) Math.Round(diff.Y / 13);
            
            ViewModel.Move(_captured, _prevPosition.X + diffX, _prevPosition.Y + diffY);
        }

        private void CanvasItemResizeMouseDown(object sender, MouseButtonEventArgs e)
        {
            var el = (FrameworkElement) sender;
            if (!(el.DataContext is ICanvasObject item))
                return;

            _captured = item;
            _prevPoint = e.GetPosition(ItemsCanvas);
            _prevPosition = (item.X, item.Y);
            _prevSize = (item.Width, item.Height);

            _resizeType = (ResizeType) Enum.Parse(typeof(ResizeType), (string) el.Tag);
            
            Mouse.Capture(el);
        }

        private void CanvasItemResizeMouseUp(object sender, MouseButtonEventArgs e)
        {
            var el = (FrameworkElement) sender;
            if (!ReferenceEquals(Mouse.Captured, el)) return;
            
            Mouse.Capture(null);
           
            ResizeItem(e.GetPosition(ItemsCanvas));
        }

        private void CanvasResizeItemMouseMove(object sender, MouseEventArgs e)
        {
            var el = (FrameworkElement) sender;
            if (Mouse.Captured == null) return;
            if (!ReferenceEquals(Mouse.Captured, el)) return;
            
            ResizeItem(e.GetPosition(ItemsCanvas));
        }
        
        private void ResizeItem(Point currentPos)
        {
            var diff = currentPos - _prevPoint;
            
            var diffX = (int) Math.Round(diff.X / 13);
            var diffY = (int) Math.Round(diff.Y / 13);

            var (x, y) = _prevPosition;
            var (w, h) = _prevSize;
            
            switch (_resizeType)
            {
                case ResizeType.N:
                case ResizeType.NW:
                case ResizeType.NE:
                    y += diffY;
                    h -= diffY;
                    break;
                
                case ResizeType.S:
                case ResizeType.SW:
                case ResizeType.SE:
                    h += diffY;
                    break;
            }
            
            switch (_resizeType)
            {
                case ResizeType.W:
                case ResizeType.NW:
                case ResizeType.SW:
                    x += diffX;
                    w -= diffX;
                    break;
                
                case ResizeType.E:
                case ResizeType.NE:
                case ResizeType.SE:
                    w += diffX;
                    break;
            }
            
            ViewModel.Resize(_captured, x, y, w, h);
        }
    }

    public class TemplateCanvasTemplateSelector : DataTemplateSelector
    {
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            var el = (FrameworkElement) container;
            
            switch (item)
            {
                case PinTemplate _:
                    return (DataTemplate) el.FindResource("PinTemplate");
                case DeadZoneTemplate _:
                    return (DataTemplate) el.FindResource("DeadZoneTemplate");
                default:
                    throw new Exception("Invalid object type");
            }
        }
    }

    public class TemplateCanvasStyleSelector : StyleSelector
    {
        public override Style SelectStyle(object item, DependencyObject container)
        {
            var el = (FrameworkElement) container;
            
            switch (item)
            {
                case PinTemplate _:
                    return (Style) el.FindResource("PinContainerStyle");
                case DeadZoneTemplate _:
                    return (Style) el.FindResource("DeadZoneContainerStyle");
                default:
                    throw new Exception("Invalid object type");
            }
        }
    }
}
