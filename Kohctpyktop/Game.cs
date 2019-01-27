﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using DColor = System.Drawing.Color;
using DPen = System.Drawing.Pen;
using DBrush = System.Drawing.Brush;
using Point = System.Windows.Point;
using Rectangle = System.Drawing.Rectangle;
using DPoint = System.Drawing.Point;

namespace Kohctpyktop
{
    public class Game : INotifyPropertyChanged, IDisposable
    {
        private readonly Renderer _renderer;
        private BitmapSource _bitmapSource;
        private SelectedTool _selectedTool;
        private DrawMode _drawMode;
        private bool _isShiftPressed;
        
        public Level Level { get; }

        public Game(Level level)
        {
            SelectedTool = SelectedTool.Silicon;
            
            Level = level;
            _renderer = new Renderer(level);
            
            RebuildModel();
        }
        
        public Game() : this(Level.CreateDummy()) {}

        public void Dispose() => _renderer.Dispose();

        public bool IsShiftPressed
        {
            get => _isShiftPressed;
            set
            {
                if (_isShiftPressed == value) return;
                _isShiftPressed = value;
                DrawMode = GetDrawMode(_selectedTool, IsShiftPressed);
                OnPropertyChanged();
            }
        }

        public (int Row, int Col) OldMouseSpot { get; set; } = (-1, -1);

        public void ProcessMouse(Point pt)
        {
            if (pt.X < 1 || pt.Y < 1) return;
            var x = (Convert.ToInt32(pt.X) - 1) / (Renderer.CellSize + 1);
            var y = (Convert.ToInt32(pt.Y) - 1) / (Renderer.CellSize + 1);
            if (OldMouseSpot.Row < 0)
            {
                DrawSinglePoint((y, x));
                OldMouseSpot = (y, x);
            }
            else
            {
                DrawLine(OldMouseSpot, (y, x));
                OldMouseSpot = (y, x);
            }
        }
        
        public void ReleaseMouse(Point pt)
        {
            OldMouseSpot = (-1, -1);
        }
        void DrawLine((int Row, int Col) from, (int Row, int Col) to)
        {
            var args = new DrawArgs(from.Row, from.Col, to.Row, to.Col);
            switch (DrawMode)
            {
                case DrawMode.Metal: DrawMetal(args);
                    break;
                case DrawMode.PType: DrawSilicon(args, true);
                    break;
                case DrawMode.NType: DrawSilicon(args, false);
                    break;
                case DrawMode.Via: PutVia(to.Row, to.Col);
                    break;
                case DrawMode.DeleteMetal: DeleteMetal(to.Row, to.Col);
                    break;
                case DrawMode.DeleteSilicon: DeleteSilicon(to.Row, to.Col);
                    break;
                case DrawMode.DeleteVia: DeleteVia(to.Row, to.Col);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        void DrawSinglePoint((int Row, int Col) pt)
        {
            DrawLine(pt, pt);
        }

        public void DrawMetal(DrawArgs args)
        {
            if (args.IsOnSingleCell)
            {
                var cell = Level.Cells[args.FromRow, args.FromCol];
                if (cell.HasMetal) return;
                Level.Cells[args.FromRow, args.FromCol].HasMetal = true;
                RebuildModel(); 
            }
            if (!args.IsBetweenNeighbors) return; //don't ruin the level!
            var fromCell = Level.Cells[args.FromRow, args.FromCol];
            var toCell = Level.Cells[args.ToRow, args.ToCol];
            var neighborInfo = fromCell.GetNeighborInfo(toCell);
            if (fromCell.HasMetal && toCell.HasMetal && neighborInfo.HasMetalLink) return;
            fromCell.HasMetal = true;
            toCell.HasMetal = true;
            fromCell.GetNeighborInfo(toCell).HasMetalLink = true; 
            RebuildModel();
        }
        static bool CanDrawP(Cell from, Cell to)
        {
            if (from.HasGate) return false;
            if (from.HasN) return false;
            var linkInfo = from.GetNeighborInfo(to);
            if (from.HasP && to.HasP && linkInfo.SiliconLink != SiliconLink.BiDirectional) return true;
            if (from.HasP && to.HasNoSilicon) return true;
            var indexForTarget = to.GetNeighborIndex(from);
            var rotatedIndex1 = (indexForTarget + 1) % 4;
            var rotatedIndex2 = (indexForTarget + 3) % 4; // modular arithmetics, bitches
            //can only draw the gate into a line of at least 3 connected N cells
            if (from.HasP && (to.HasN || to.HasNGate) && to.NeighborInfos[rotatedIndex1]?.SiliconLink == SiliconLink.BiDirectional &&
                to.NeighborInfos[rotatedIndex2]?.SiliconLink == SiliconLink.BiDirectional) return true;
            return false;
        }
        static bool CanDrawN(Cell from, Cell to)
        {
            if (from.HasGate) return false;
            if (from.HasP) return false;
            var linkInfo = from.GetNeighborInfo(to);
            if (from.HasN && to.HasN && linkInfo.SiliconLink != SiliconLink.BiDirectional) return true;
            if (from.HasN && to.HasNoSilicon) return true;
            var indexForTarget = to.GetNeighborIndex(from);
            var rotatedIndex1 = (indexForTarget + 1) % 4;
            var rotatedIndex2 = (indexForTarget + 3) % 4; // modular arithmetics, bitches
            //can only draw the gate into a line of at least 3 connected N cells
            if (from.HasN && (to.HasP || to.HasPGate) && to.NeighborInfos[rotatedIndex1]?.SiliconLink == SiliconLink.BiDirectional &&
                to.NeighborInfos[rotatedIndex2]?.SiliconLink == SiliconLink.BiDirectional) return true;
            return false;
        }
        public void DrawSilicon(DrawArgs args, bool isPType)
        {
            if (args.IsOnSingleCell)
            {
                var cell = Level.Cells[args.FromRow, args.FromCol];
                if (cell.SiliconLayerContent != SiliconTypes.None) return;
                Level.Cells[args.FromRow, args.FromCol].SiliconLayerContent = 
                    isPType ? SiliconTypes.PType : SiliconTypes.NType;
                RebuildModel();
                return;
            }
            if (!args.IsBetweenNeighbors) return; //don't ruin the level!
            var fromCell = Level.Cells[args.FromRow, args.FromCol];
            var toCell = Level.Cells[args.ToRow, args.ToCol];
            var neighborInfo = fromCell.GetNeighborInfo(toCell);
            if (isPType && CanDrawP(fromCell, toCell))
            {
                if (toCell.HasNoSilicon)
                {
                    toCell.SiliconLayerContent = SiliconTypes.PType;
                    neighborInfo.SiliconLink = SiliconLink.BiDirectional;
                }
                else if (toCell.HasP)
                {
                    neighborInfo.SiliconLink = SiliconLink.BiDirectional;
                }
                else if (toCell.HasN || toCell.HasNGate)
                {
                    //the gate direction is perpendicular to the link direction
                    toCell.SiliconLayerContent = toCell.IsHorizontalNeighborOf(fromCell)
                        ? SiliconTypes.NTypeVGate : SiliconTypes.NTypeHGate;
                    neighborInfo.SiliconLink = SiliconLink.Master; //from cell is the master cell
                }
                else throw new InvalidOperationException("You missed a case here!");
                RebuildModel();
                return;
            }
            if (!isPType && CanDrawN(fromCell, toCell))
            {
                if (toCell.HasNoSilicon)
                {
                    toCell.SiliconLayerContent = SiliconTypes.NType;
                    neighborInfo.SiliconLink = SiliconLink.BiDirectional;
                }
                else if (toCell.HasN)
                {
                    neighborInfo.SiliconLink = SiliconLink.BiDirectional;
                }
                else if (toCell.HasP || toCell.HasPGate)
                {
                    //the gate direction is perpendicular to the link direction
                    toCell.SiliconLayerContent = toCell.IsHorizontalNeighborOf(fromCell)
                        ? SiliconTypes.PTypeVGate : SiliconTypes.PTypeHGate;
                    neighborInfo.SiliconLink = SiliconLink.Master; //from cell is the master cell
                }
                else throw new InvalidOperationException("You missed a case here!");
                RebuildModel();
                return;
            }


            RebuildModel();
            
        }
        public void PutVia(int row, int col)
        {
            var cell = Level.Cells[row, col];
            if (cell.HasP)
            {
                cell.SiliconLayerContent = SiliconTypes.PTypeVia;
                RebuildModel();
            }
            else if (cell.HasN)
            {
                cell.SiliconLayerContent = SiliconTypes.NTypeVia;
                RebuildModel();
            }
        }
        public void DeleteMetal(int row, int col)
        {
            var cell = Level.Cells[row, col];
            if (cell.HasMetal)
            {
                foreach (var ni in cell.NeighborInfos)
                {
                    if (ni != null) ni.HasMetalLink = false;
                }
                cell.HasMetal = false;
                RebuildModel();
            }
        }
        private static Dictionary<SiliconTypes, SiliconTypes> DeleteSiliconDic { get; } = new Dictionary<SiliconTypes, SiliconTypes>
        {
            { SiliconTypes.NTypeHGate, SiliconTypes.NType },
            { SiliconTypes.NTypeVGate, SiliconTypes.NType },
            { SiliconTypes.PTypeHGate, SiliconTypes.PType },
            { SiliconTypes.PTypeVGate, SiliconTypes.PType }
        };
        
        public void DeleteSilicon(int row, int col)
        {
            var cell = Level.Cells[row, col];
            if (cell.HasNoSilicon) return;
            foreach (var ni in cell.NeighborInfos)
            {
                if (ni.SiliconLink != SiliconLink.None) ni.SiliconLink = SiliconLink.None;
                if (ni.ToCell.HasGate)
                {
                    ni.ToCell.SiliconLayerContent = DeleteSiliconDic[ni.ToCell.SiliconLayerContent];
                    
                    foreach (var innerNi in ni.ToCell.NeighborInfos)
                    {
                        if (innerNi.SiliconLink == SiliconLink.Slave) innerNi.SiliconLink = SiliconLink.None;
                    }
                }
            }
            cell.SiliconLayerContent = SiliconTypes.None;
            RebuildModel();
        }
        public void DeleteVia(int row, int col)
        {
            var cell = Level.Cells[row, col];
            if (cell.SiliconLayerContent == SiliconTypes.PTypeVia)
            {
                cell.SiliconLayerContent = SiliconTypes.PType;
                RebuildModel();
            }
            if (cell.SiliconLayerContent == SiliconTypes.NTypeVia)
            {
                cell.SiliconLayerContent = SiliconTypes.NType;
                RebuildModel();
            }
        }

        public BitmapSource BitmapSource
        {
            get => _bitmapSource;
            set
            {
                if (Equals(value, _bitmapSource)) return;
                _bitmapSource = value;
                OnPropertyChanged();
            }
        }
        
        public void RebuildModel()
        {
            _renderer.Render();
            
            var bmpImage = new BitmapImage();
            var stream = new MemoryStream();
            _renderer.Bitmap.Save(stream, ImageFormat.Bmp);
            bmpImage.BeginInit();
            bmpImage.StreamSource = stream;
            bmpImage.EndInit();
            bmpImage.Freeze();
            BitmapSource = bmpImage;
            //if (LevelModel != null)
            //{
            //    var old = LevelModel;
            //    LevelModel = null; //force rebind
            //    LevelModel = old;
            //    return;
            //}
            //var result = new List<List<Cell>>();
            //for (var i = 0; i < Level.Cells.GetLength(0); i++)
            //{
            //    var row = new List<Cell>();
            //    for (var j = 0; j < Level.Cells.GetLength(1); j++)
            //    {
            //        row.Add(Level.Cells[i,j]);
            //    }
            //    result.Add(row);
            //}
            //LevelModel = result;
        }

        private static DrawMode GetDrawMode(SelectedTool tool, bool isShiftHeld)
        {
            switch (tool)
            {
                case SelectedTool.AddOrDeleteVia: return isShiftHeld ? DrawMode.DeleteVia : DrawMode.Via;
                case SelectedTool.Metal: return DrawMode.Metal;
                case SelectedTool.Silicon: return isShiftHeld ? DrawMode.PType : DrawMode.NType;
                case SelectedTool.DeleteMetalOrSilicon:
                    return isShiftHeld ? DrawMode.DeleteMetal : DrawMode.DeleteSilicon;
                default: throw new ArgumentException("Invalid tool type");
            }
        }

        public DrawMode DrawMode
        {
            get => _drawMode;
            set
            {
                if (value == _drawMode) return;
                _drawMode = value;
                OnPropertyChanged();
            }
        }

        public SelectedTool SelectedTool
        {
            get => _selectedTool;
            set
            {
                if (value == _selectedTool) return;
                _selectedTool = value;
                DrawMode = GetDrawMode(_selectedTool, IsShiftPressed);
                OnPropertyChanged();
            }
        }

        #region PropertyChanged



        public event PropertyChangedEventHandler PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
     
}
