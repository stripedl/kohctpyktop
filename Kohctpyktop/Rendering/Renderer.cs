using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using Kohctpyktop.Input;
using Kohctpyktop.Models;
using Kohctpyktop.Models.Field;

namespace Kohctpyktop.Rendering
{
    public class Renderer : IDisposable
    {
        private readonly ILayer _layer;
        private readonly Graphics _graphics;
        
        public static readonly int CellSize = 12;
        private const int ViaSize = 6;
        
        private static readonly Color BorderColor = Color.Black;
        private static readonly Color BgColor = "959595".AsDColor();
        private static readonly Brush PBrush = new SolidBrush("FFF6FF00".AsDColor());
        private static readonly Brush NBrush = new SolidBrush("FFB60000".AsDColor());
        private static readonly Brush PGateBrush = new SolidBrush("FF860000".AsDColor());
        private static readonly Brush NGateBrush = new SolidBrush("FFEDC900".AsDColor());
        private static readonly Brush MetalBrush = new SolidBrush("80FFFFFF".AsDColor());
        private static readonly Brush LockedRegionBrush = new SolidBrush("20000000".AsDColor());
        private static readonly Brush TopologyBrush = new SolidBrush("30FF0000".AsDColor());
        private static readonly Brush TopologyMetalBrush = new SolidBrush("60FF0000".AsDColor());
        private static readonly Brush TopologySiliconBrush = new SolidBrush("600000FF".AsDColor());
        private static readonly Brush BorderBrush = new SolidBrush(BorderColor);
        private static readonly Pen GridPen = new Pen(Color.FromArgb(40, Color.Black));
        private static readonly Pen BorderPen = new Pen(BorderBrush);
        private static readonly Pen PPen = new Pen(PBrush);
        private static readonly Pen NPen = new Pen(NBrush);
        private static readonly Pen MetalPen = new Pen(MetalBrush);
        private static readonly Pen TopologyMetalPen = new Pen(TopologyMetalBrush);
        private static readonly Pen TopologySiliconPen = new Pen(TopologySiliconBrush);
        
        public Bitmap Bitmap { get; }
        
        public Renderer(ILayer layer)
        {
            _layer = layer;
            Bitmap = new Bitmap(
                (CellSize + 1) * _layer.Width + 1, 
                (CellSize + 1) * _layer.Height + 1);
            _graphics = Graphics.FromImage(Bitmap);
            _graphics.CompositingQuality = CompositingQuality.HighSpeed; // quality is not required actually, rendering is pixel-perfect
            _graphics.InterpolationMode = InterpolationMode.NearestNeighbor; // this too
            _graphics.SmoothingMode = SmoothingMode.None; // causes artifacts
            _graphics.PixelOffsetMode = PixelOffsetMode.None; // this too
        }
        
        void DrawGrid()
        {
            var w = Bitmap.Width;
            var h = Bitmap.Height;
            
            for (var i = 0; i <= Math.Max(_layer.Width, _layer.Height) * (CellSize + 1); i += CellSize + 1)
            {
                _graphics.DrawLine(GridPen, i, 0, i, h);
                _graphics.DrawLine(GridPen, 0, i, w, i);
            }
        }
        
        private const int CellInsets = 2;

        private static Rectangle GetCellBounds(int x, int y)
        {
            return new Rectangle(1 + x * (CellSize + 1), 1 + y * (CellSize + 1), CellSize, CellSize);
        }

        [Flags]
        public enum Corner
        {
            Near = 0, 
            FarX = 1, 
            FarY = 2, 
            Far = FarX | FarY
        }
        
        private void FillMid(Brush brush, Rectangle cellBounds)
        {
            cellBounds.Inflate(-CellInsets, -CellInsets);
            _graphics.FillRectangle(brush, cellBounds);
        }

        private static bool IsVerticalSide(Side side) => side == Side.Bottom || side == Side.Top;

        private static bool IsSideFarFromBoundsOrigin(Side side) => side == Side.Right || side == Side.Bottom;

        private static (Rectangle Rect, Rectangle NearToBounds, Rectangle NearToCenter) GetCellSideBounds(int originX, int originY, Side side)
        {
            var isFar = IsSideFarFromBoundsOrigin(side);

            var rectOfs = isFar ? CellSize - CellInsets : 0;
            var lineOfs = isFar ? CellSize - 1 : 0;
            var centerOfs = isFar ? CellSize - CellInsets : CellInsets - 1;

            return IsVerticalSide(side)
                ? (new Rectangle(originX + CellInsets, originY + rectOfs, CellSize - 2 * CellInsets, CellInsets),
                    new Rectangle(originX + CellInsets, originY + lineOfs, CellSize - 2 * CellInsets, 1),
                    new Rectangle(originX + CellInsets, originY + centerOfs, CellSize - 2 * CellInsets, 1))
                : (new Rectangle(originX + rectOfs, originY + CellInsets, CellInsets, CellSize - 2 * CellInsets),
                    new Rectangle(originX + lineOfs, originY + CellInsets, 1, CellSize - 2 * CellInsets),
                    new Rectangle(originX + centerOfs, originY + CellInsets, 1, CellSize - 2 * CellInsets));
        }
        
        private static (Point NearToCenter, Point NearToBounds, Point NearHorzLink, Point NearVertLink) 
            GetCellCornerBounds(int originX, int originY, Corner corner)
        {
            var isFarX = corner.HasFlag(Corner.FarX);
            var isFarY = corner.HasFlag(Corner.FarY);

            var nearHorz = originX + (isFarX ? CellSize - 1 : 0);
            var nearVert = originY + (isFarY ? CellSize - 1 : 0);
            var farHorz = originX + (isFarX ? CellSize - CellInsets : CellInsets - 1);
            var farVert = originY + (isFarY ? CellSize - CellInsets : CellInsets - 1);

            return (
                new Point(farHorz, farVert),
                new Point(nearHorz, nearVert),
                new Point(nearHorz, farVert),
                new Point(farHorz, nearVert));
        }

        private static (Pen NonGatePen, Brush NonGateBrush, Brush GateBrush) SelectSiliconBrush(ILayerCell cell)
        {
            return cell.IsBaseP()
                ? cell.IsBaseN()
                    ? throw new InvalidOperationException("both P and N silicon on single cell")
                    : (PPen, PBrush, PGateBrush)
                : cell.IsBaseN()
                    ? (NPen, NBrush, NGateBrush)
                    : throw new InvalidOperationException("no silicon on cell");
        }

        private static Side GetOppositeSide(Side side)
        {
            switch (side)
            {
                case Side.Top: return Side.Bottom;
                case Side.Bottom: return Side.Top;
                case Side.Left: return Side.Right;
                case Side.Right: return Side.Left;
                default: throw new InvalidOperationException("invalid side " + side); 
            }
        }
        
        private void SiliconCellSide(ILayerCell cell, Side side, Rectangle cellBounds, bool isSideDetached)
        {
            var (_, brush, gateBrush) = SelectSiliconBrush(cell);

            var link = cell.Links[side]?.SiliconLink ?? SiliconLink.None;
            var oppositeLink = cell.Links[GetOppositeSide(side)]?.SiliconLink ?? SiliconLink.None;
            var hasSlaveLinkInDimension = link == SiliconLink.Slave || oppositeLink == SiliconLink.Slave;
            
            var actualBrush = hasSlaveLinkInDimension ? gateBrush : brush;
            
            var (rect, nearToBounds, nearToCenter) = GetCellSideBounds(cellBounds.X, cellBounds.Y, side);
            _graphics.FillRectangle(actualBrush, rect);
            
            if (isSideDetached || link == SiliconLink.None || hasSlaveLinkInDimension) 
                _graphics.FillRectangle(BorderBrush, nearToBounds);
            if (!isSideDetached && cell.HasGate() && !hasSlaveLinkInDimension)
                _graphics.FillRectangle(BorderBrush, nearToCenter);
        }
        
        private void MetalCellSide(ILayerCell cell, Side side, Rectangle cellBounds, bool isSideDetached)
        {
            var (rect, nearToBounds, _) = GetCellSideBounds(cellBounds.X, cellBounds.Y, side);
            _graphics.FillRectangle(MetalBrush, rect);
            
            if (isSideDetached || !(cell.Links[side]?.HasMetalLink ?? false))
                _graphics.FillRectangle(BorderBrush, nearToBounds);
        }

        private void GenericCellCorner(bool hasHorzLink, bool hasVertLink, Pen pen,
            Point nearToCenter, Point nearToBounds, Point nearHorzLink, Point nearVertLink)
        {
            if (hasHorzLink && hasVertLink)
            {
                // that won't work on insets larger than 2 (System.Drawing sucks)
                _graphics.DrawPolygon(pen, new[] { nearToCenter, nearHorzLink, nearToBounds, nearVertLink });
                SetPixel(nearToBounds.X, nearToBounds.Y, BorderColor);
            }
            else if (hasHorzLink)
            {
                _graphics.DrawPolygon(pen, new[] { nearToCenter, nearHorzLink, nearToBounds, nearVertLink });
                _graphics.DrawLine(BorderPen, nearToBounds, nearVertLink);
            }
            else if (hasVertLink)
            {
                _graphics.DrawPolygon(pen, new[] { nearToCenter, nearHorzLink, nearToBounds, nearVertLink });
                _graphics.DrawLine(BorderPen, nearHorzLink, nearToBounds);
            }
            else
            {
                _graphics.DrawPolygon(pen, new[] { nearHorzLink, nearVertLink, nearToCenter });
                _graphics.DrawLine(BorderPen, nearHorzLink, nearVertLink);
            }
        }

        private void SiliconCellCorner(ILayerCell cell, Corner corner, Rectangle cellBounds, bool isVertCornerDetached, 
            bool isHorzCornerDetached)
        {
            var (pen, _, _) = SelectSiliconBrush(cell);

            var (nearToCenter, nearToBounds, nearHorzLink, nearVertLink) = GetCellCornerBounds(cellBounds.X, cellBounds.Y, corner);
            
            var horzNeigh = cell.Links[corner.HasFlag(Corner.FarX) ? 2 : 0];
            var vertNeigh = cell.Links[corner.HasFlag(Corner.FarY) ? 3 : 1];

            var hasHorzLink = !isHorzCornerDetached && (horzNeigh?.SiliconLink ?? SiliconLink.None) != SiliconLink.None;
            var hasVertLink = !isVertCornerDetached && (vertNeigh?.SiliconLink ?? SiliconLink.None) != SiliconLink.None;

            GenericCellCorner(hasHorzLink, hasVertLink, pen,
                nearToCenter, nearToBounds, nearHorzLink, nearVertLink);

            if (!cell.HasGate()) return;

            var oppositeVertNeigh = cell.Links[corner.HasFlag(Corner.FarY) ? 1 : 3];
            var isVerticalGate =
                (vertNeigh?.SiliconLink ?? SiliconLink.None) == SiliconLink.Slave ||
                (oppositeVertNeigh?.SiliconLink ?? SiliconLink.None) == SiliconLink.Slave;
            var srcPoint = isVerticalGate ? nearVertLink : nearHorzLink;
            _graphics.DrawLine(BorderPen, srcPoint, nearToCenter);
        }

        private void MetalCellCorner(ILayerCell cell, Corner corner, Rectangle cellBounds, bool isVertCornerDetached, 
            bool isHorzCornerDetached)
        {
            var (nearToCenter, nearToBounds, nearHorzLink, nearVertLink) = GetCellCornerBounds(cellBounds.X, cellBounds.Y, corner);
            
            var horzNeigh = cell.Links[corner.HasFlag(Corner.FarX) ? 2 : 0];
            var vertNeigh = cell.Links[corner.HasFlag(Corner.FarY) ? 3 : 1];

            var hasHorzLink = !isHorzCornerDetached && (horzNeigh?.HasMetalLink ?? false);
            var hasVertLink = !isVertCornerDetached && (vertNeigh?.HasMetalLink ?? false);

            GenericCellCorner(hasHorzLink, hasVertLink, MetalPen, 
                nearToCenter, nearToBounds, nearHorzLink, nearVertLink);
        }

        private void GenericIntercellular(Rectangle cellBounds, Pen pen, bool isVertical)
        {
            if (isVertical)
            {
                SetPixel(cellBounds.Left, cellBounds.Top - 1, BorderColor);
                SetPixel(cellBounds.Right - 1, cellBounds.Top - 1, BorderColor);
                _graphics.DrawLine(pen, 
                    cellBounds.Left + 1, 
                    cellBounds.Top - 1, 
                    cellBounds.Right - 2,
                    cellBounds.Top - 1);
            } 
            else 
            {
                SetPixel(cellBounds.Left - 1, cellBounds.Top, BorderColor);
                SetPixel(cellBounds.Left - 1, cellBounds.Bottom - 1, BorderColor);
                _graphics.DrawLine(pen, 
                    cellBounds.Left - 1, 
                    cellBounds.Top + 1, 
                    cellBounds.Left - 1,
                    cellBounds.Bottom - 2);
            }
        }

        private void SetPixel(int x, int y, Color color)
        {
            if (x < 0 || x >= Bitmap.Width || y < 0 || y >= Bitmap.Height) return;
            
            Bitmap.SetPixel(x, y, color);
        }
        
        private void SiliconIntercellular(ILayerCell cell, bool isVertical, SiliconLink siliconLink, Rectangle cellBounds)
        {
            if (siliconLink == SiliconLink.None) return;

            Pen pen;

            if (cell.HasN())
	            pen = NPen;
            else if (cell.HasP())
	            pen = PPen;
            else if (siliconLink != SiliconLink.Slave ^ cell.HasPGate())
	            pen = NPen;
            else
	            pen = PPen;

            GenericIntercellular(cellBounds, pen, isVertical);
        }
        
        private void MetalIntercellular(ILayerCell cell, bool isVertical, Rectangle cellBounds)
        {
            if (cell.Links[isVertical ? 1 : 0]?.HasMetalLink ?? false)
                GenericIntercellular(cellBounds, MetalPen, isVertical);
        }

        static readonly Font PinNameFont = new Font("Courier New", 8);

        private void DrawCell(RenderOpts opts, int i, int j, Position from, Position to, List<ILayerCell> namedCells)
        {
            var cell = _layer.Cells[i, j];
            var bounds = GetCellBounds(j, i);

            var isInXRange = j >= from.X && j < to.X;
            var isInYRange = i >= from.Y && i < to.Y;

            if (isInXRange && isInYRange)
            {
                bounds.Offset(opts.Selection.DragOffsetX, opts.Selection.DragOffsetY);
            }

            var isLeftSideDetached = (j == to.X || j == from.X) && isInYRange;
            var isTopSideDetached = (i == to.Y || i == from.Y) && isInXRange;
            var isRightSideDetached = (j == to.X - 1 || j == from.X - 1) && isInYRange;
            var isBottomSideDetached = (i == to.Y - 1 || i == from.Y - 1) && isInXRange;

            if (cell.HasSilicon())
            {
                var (_, brush, gateBrush) = SelectSiliconBrush(cell);
                FillMid(cell.HasGate() ? gateBrush : brush, bounds);
                SiliconCellSide(cell, Side.Top, bounds, isTopSideDetached);
                SiliconCellSide(cell, Side.Bottom, bounds, isBottomSideDetached);
                SiliconCellSide(cell, Side.Left, bounds, isLeftSideDetached);
                SiliconCellSide(cell, Side.Right, bounds, isRightSideDetached);

                SiliconCellCorner(cell, Corner.Near, bounds, isTopSideDetached, isLeftSideDetached);
                SiliconCellCorner(cell, Corner.FarX, bounds, isTopSideDetached, isRightSideDetached);
                SiliconCellCorner(cell, Corner.FarY, bounds, isBottomSideDetached, isLeftSideDetached);
                SiliconCellCorner(cell, Corner.Far, bounds, isBottomSideDetached, isRightSideDetached);

                if (!isLeftSideDetached)
                    SiliconIntercellular(cell, false, cell.Links[0]?.SiliconLink ?? SiliconLink.None, bounds);
                if (!isTopSideDetached)
                    SiliconIntercellular(cell, true, cell.Links[1]?.SiliconLink ?? SiliconLink.None, bounds);

                if (cell.HasVia()) // in original game vias displaying under metal layer
                {
                    var viaX = bounds.X + (bounds.Width - ViaSize) / 2;
                    var viaY = bounds.Y + (bounds.Height - ViaSize) / 2;

                    _graphics.DrawLine(BorderPen, viaX + 1, viaY, viaX + ViaSize - 2, viaY);
                    _graphics.DrawLine(BorderPen, viaX + 1, viaY + ViaSize - 1, viaX + ViaSize - 2,
                        viaY + ViaSize - 1);
                    _graphics.DrawLine(BorderPen, viaX, viaY + 1, viaX, viaY + ViaSize - 2);
                    _graphics.DrawLine(BorderPen, viaX + ViaSize - 1, viaY + 1, viaX + ViaSize - 1,
                        viaY + ViaSize - 2);
                }
            }

            if (cell.HasMetal)
            {
                FillMid(MetalBrush, bounds);

                MetalCellSide(cell, Side.Top, bounds, isTopSideDetached);
                MetalCellSide(cell, Side.Bottom, bounds, isBottomSideDetached);
                MetalCellSide(cell, Side.Left, bounds, isLeftSideDetached);
                MetalCellSide(cell, Side.Right, bounds, isRightSideDetached);

                MetalCellCorner(cell, Corner.Near, bounds, isTopSideDetached, isLeftSideDetached);
                MetalCellCorner(cell, Corner.FarX, bounds, isTopSideDetached, isRightSideDetached);
                MetalCellCorner(cell, Corner.FarY, bounds, isBottomSideDetached, isLeftSideDetached);
                MetalCellCorner(cell, Corner.Far, bounds, isBottomSideDetached, isRightSideDetached);

                if (!isLeftSideDetached)
                    MetalIntercellular(cell, false, bounds);
                if (!isTopSideDetached)
                    MetalIntercellular(cell, true, bounds);
            }
            if (opts.Assignments != null)
            {
                var assignments = opts.Assignments[i, j];
                if ((assignments.LastAssignedSiliconNode == opts.HoveredNode ||
                     assignments.LastAssignedMetalNode == opts.HoveredNode) && opts.HoveredNode != null)
                {
                    var fillBounds = bounds;
                    fillBounds.Inflate(-1, -1);
                    FillTopologyPlace(fillBounds, assignments.LastAssignedMetalNode == opts.HoveredNode);
                }
            }

            if (cell.Pin != null)
            {
                namedCells.Add(cell);
            }
        }
        
        private void DrawSiliconAndMetal(RenderOpts opts)
        {
            var namedCells = new List<ILayerCell>();
            
            var (from, to) = opts.Selection?.ToFieldPositions() ?? default((Position, Position));
            
            for (var i = 0; i < _layer.Height; i++)
            for (var j = 0; j < _layer.Width; j++)
            {
                if (opts.SelectionState != SelectionState.None)
                {
                    var isInXRange = j >= from.X && j < to.X;
                    var isInYRange = i >= from.Y && i < to.Y;

                    if (!isInXRange || !isInYRange) DrawCell(opts, i, j, from, to, namedCells);
                }
                else DrawCell(opts, i, j, from, to, namedCells);
            }
            
            // dragging cells should be rendered over
            if (opts.SelectionState != SelectionState.None)
            {
                for (var i = from.Y; i < to.Y; i++)
                for (var j = from.X; j < to.X; j++)
                {
                    DrawCell(opts, i, j, from, to, namedCells);
                }
            }

            foreach (var zone in _layer.Template.DeadZones)
            {
                var zoneRect = new Rectangle(
                    zone.Origin.X * (CellSize + 1), 
                    zone.Origin.Y * (CellSize + 1),
                    zone.Width * (CellSize + 1) + 1,
                    zone.Height * (CellSize + 1) + 1);
                
                _graphics.FillRectangle(LockedRegionBrush, zoneRect);
            }

            foreach (var cell in namedCells)
            {
                var pin = cell.Pin;
                
                var bounds = GetCellBounds(cell.Column, cell.Row);
                bounds.Width += CellSize * (pin.Width - 1);
                bounds.Height += CellSize * (pin.Height - 1);
                bounds.X += 1;
                bounds.Y += 1;
                var centerX = bounds.Left + bounds.Width / 2;
                var centerY = bounds.Top + bounds.Height / 2;
                _graphics.FillRectangle(Brushes.WhiteSmoke, bounds);
                var name = cell.Pin?.Name ?? "XZ";
                var measure = _graphics.MeasureString(name, PinNameFont);
                
                if (opts.Assignments != null)
                {
                    var assignments = opts.Assignments[cell.Row, cell.Column];
                    if ((assignments.LastAssignedSiliconNode == opts.HoveredNode ||
                         assignments.LastAssignedMetalNode == opts.HoveredNode) && opts.HoveredNode != null)
                    {
                        var fillBounds = bounds;
                        fillBounds.Inflate(-1, -1);
                        FillTopologyPlace(fillBounds, assignments.LastAssignedMetalNode == opts.HoveredNode);
                    }
                }
                
                _graphics.DrawString(name, PinNameFont, Brushes.Black, centerX - measure.Width / 2,
                    centerY - measure.Height / 2);
            }
        }

        public void FillTopologyPlace(Rectangle bounds, bool metal)
        {
            for (int i = 0; i < Math.Max(bounds.Width, bounds.Height); i += 3)
            {
                if (metal)
                {
                    _graphics.DrawLine(TopologyMetalPen, bounds.Left + i, bounds.Top,
                        bounds.Right, bounds.Bottom - i);
                    if(i!=0)
                    _graphics.DrawLine(TopologyMetalPen, bounds.Left, bounds.Top + i,
                        bounds.Right - i, bounds.Bottom);
                }
                else
                {
                    _graphics.DrawLine(TopologySiliconPen, bounds.Right - i, bounds.Top,
                        bounds.Left, bounds.Bottom - i);
                    if (i != 0)
                        _graphics.DrawLine(TopologySiliconPen, bounds.Right, bounds.Top + i,
                            bounds.Left + i, bounds.Bottom);
                }
            }
        }


        public void Render(RenderOpts opts)
        {
            _graphics.Clear(BgColor);
            DrawGrid();
            DrawSiliconAndMetal(opts);
            
            if (opts.SelectionState != SelectionState.None)
                DrawSelectionBorder(opts.Selection);
        }

        private void DrawSelectionBorder(Selection selection)
        {
            var (from, to) = selection.ToFieldPositions();

            var width = to.X - from.X;
            var height = to.Y - from.Y;

            var realCellSize = CellSize + 1;

            _graphics.DrawRectangle(Pens.White, 
                selection.DragOffsetX + from.X * realCellSize, 
                selection.DragOffsetY + from.Y * realCellSize, 
                width * realCellSize,
                height * realCellSize);
        }

        public void Dispose()
        {
            _graphics?.Dispose();
            Bitmap?.Dispose();
        }
    }
}