﻿#region Using directives
using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Windows.Forms;
using System.Drawing;
using System.IO;

using Sharp3D.Math.Core;

using treeDiM.StackBuilder.Basics;
using treeDiM.StackBuilder.Graphics;

using Microsoft.Office.Core;
using Microsoft.Office.Interop.Excel;
#endregion

namespace treeDiM.StackBuilder.Desktop
{
    public class DocumentSB : Document, IDocument
    {
        #region Data members
        private string _filePath;
        private bool _dirty = false;
        private List<IView> _views = new List<IView>();
        private IView _activeView;
        public event EventHandler Modified;
        #endregion

        #region Constructor
        public DocumentSB(string filePath, IDocumentListener listener)
            :base(filePath, listener)
        {
            _filePath = filePath;
            _dirty = false;
        }
        public DocumentSB(string name, string description, string author, IDocumentListener listener)
            : base(name, description, author, DateTime.Now, listener)
        {
            _dirty = false;
        }
        #endregion

        #region Public properties
        public string FilePath
        {
            get { return _filePath; }
            set { _filePath = value; }
        }
        #endregion

        #region IDocument implementation
        public bool IsDirty { get { return _dirty; } }
        public bool IsNew { get { return string.IsNullOrEmpty(_filePath); } }
        public bool HasValidPath  {   get { return System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(_filePath)); } }
        public void Save()
        {
            if (IsNew) return;
            if (!HasValidPath)
                throw new System.IO.DirectoryNotFoundException(
                    string.Format("Directory {0} could not be found!", System.IO.Path.GetDirectoryName(_filePath))
                    );
            Write(_filePath);
            _dirty = false;
        }

        public void SaveAs(string filePath)
        {
            _filePath = filePath;
            Save();
        }

        public void Close(CancelEventArgs e)
        {
            while (_views.Count > 0)
                RemoveView(_views[0]);
            base.Close();
        }

        public override void Modify()
        {
            _dirty = true;
            Modified?.Invoke(this, new EventArgs());
        }

        public List<IView> Views { get { return _views; } }

        public IView ActiveView
        {
            set
            {
                _activeView = value;
                _activeView.Activate();
            }
            get
            {
                return _activeView;
            }
        }

        public void AddView(IView view)
        {
            _views.Add(view);
        }

        public void RemoveView(IView view)
        {
            // remove from list of attached views
            _views.Remove(view);
        }
        #endregion

        #region View creation methods
        public DockContentView CreateViewAnalysis(Analysis analysis)
        {
            DockContentView form = null;
            if (analysis is AnalysisCasePallet) form = new DockContentAnalysisCasePallet(this, analysis as AnalysisCasePallet);
            else if (analysis is AnalysisBoxCase) form = new DockContentAnalysisBoxCase(this, analysis as AnalysisBoxCase);
            else if (analysis is AnalysisCylinderPallet) form = new DockContentAnalysisCylinderPallet(this, analysis as AnalysisCylinderPallet);
            else if (analysis is AnalysisCylinderCase) form = new DockContentAnalysisCylinderCase(this, analysis as AnalysisCylinderCase);
            else if (analysis is AnalysisPalletTruck) form = new DockContentAnalysisPalletTruck(this, analysis as AnalysisPalletTruck);
            else if (analysis is AnalysisCaseTruck) form = new DockContentAnalysisCaseTruck(this, analysis as AnalysisCaseTruck);
            else if (analysis is AnalysisCylinderTruck) form = new DockContentAnalysisCylinderTruck(this, analysis as AnalysisCylinderTruck);
            else if (analysis is AnalysisHCylPallet) form = new DockContentAnalysisHCylPallet(this, analysis as AnalysisHCylPallet);
            else
            {
                _log.Error(string.Format("Analysis ({0}) type not handled", analysis.Name));
                return null;
            }
            AddView(form);
            return form;
        }
        public DockContentView CreateViewHAnalysis(AnalysisHetero analysis)
        {
            DockContentView form = null;
            if (analysis is HAnalysisPallet) form = new DockContentHAnalysisCasePallet(this, analysis);
            else if (analysis is HAnalysisTruck) form = new DockContentHAnalysisCaseTruck(this, analysis);
            else
            {
                _log.Error(string.Format("Analysis ({0}) type not handled", analysis.Name));
                return null;
            }
            AddView(form);
            return form;
        }
        /*
        /// <summary>
        /// creates a new DockContentECTAnalysis view
        /// </summary>
        public DockContentECTAnalysis CreateECTAnalysisView(ECTAnalysis analysis)
        {
            DockContentECTAnalysis form = new DockContentECTAnalysis(this, analysis);
            AddView(form);
            return form;
        }
        */
        #endregion

        #region UI item creation
        /// <summary>
        /// Creates a new BoxProperties object with MODE_BOX
        /// </summary>
        public void CreateNewBoxUI()
        {
            using (FormNewBox form = new FormNewBox(this, FormNewBox.Mode.BOX))
            {
                if (DialogResult.OK == form.ShowDialog())
                {
                    BoxProperties boxProperties = CreateNewBox(form.BoxName, form.Description
                        , form.BoxLength, form.BoxWidth, form.BoxHeight
                        , form.Weight, form.Colors);
                    boxProperties.TextureList = form.TextureList;
                }
            }
        }
        /// <summary>
        /// Creates a new BoxProperties object with MODE_CASE
        /// </summary>
        public void CreateNewCaseUI()
        {
            using (FormNewBox form = new FormNewBox(this, FormNewBox.Mode.CASE))
            {
                if (DialogResult.OK == form.ShowDialog())
                {
                    BoxProperties boxProperties = CreateNewCase(form.BoxName, form.Description
                        , form.BoxLength, form.BoxWidth, form.BoxHeight
                        , form.InsideLength, form.InsideWidth, form.InsideHeight
                        , form.Weight, form.Colors);
                    boxProperties.TapeColor = form.TapeColor;
                    boxProperties.TapeWidth = form.TapeWidth;
                    boxProperties.TextureList = form.TextureList;
                    boxProperties.StrapperSet = form.StrapperSet;
                }
            }
        }
        /// <summary>
        /// Creates a new PackProperties object
        /// </summary>
        public void CreateNewPackUI()
        {
            using (FormNewPack form = new FormNewPack(this, null) { Boxes = Boxes.ToList() })
            {
                if (DialogResult.OK == form.ShowDialog())
                {
                    PackProperties packProperties = CreateNewPack(
                        form.ItemName, form.ItemDescription
                        , form.SelectedBox
                        , form.Arrangement
                        , form.BoxOrientation
                        , form.Wrapper);
                    if (form.HasForcedOuterDimensions)
                        packProperties.ForceOuterDimensions(form.OuterDimensions);
                    packProperties.StrapperSet = form.StrapperSet;
                }
            }
        }

        public void CreateNewCylinderUI()
        {
            using (FormNewCylinder form = new FormNewCylinder(this, null))
            {
                if (DialogResult.OK == form.ShowDialog())
                    CreateNewCylinder(
                        form.ItemName, form.ItemDescription
                        , form.RadiusOuter, form.RadiusInner, form.CylinderHeight, form.Weight
                        , form.ColorTop, form.ColorWallOuter, form.ColorWallInner);
            }

        }
        /// <summary>
        /// Creates a new BundleProperties object
        /// </summary>
        public void CreateNewBundleUI()
        {
            using (FormNewBundle form = new FormNewBundle(this))
            {
                if (DialogResult.OK == form.ShowDialog())
                    CreateNewBundle(
                        form.BundleName, form.Description
                        , form.BundleLength, form.BundleWidth, form.UnitThickness
                        , form.UnitWeight
                        , form.Color
                        , form.NoFlats);
            }
        }
        /// <summary>
        /// Creates a new InterlayerProperties object
        /// </summary>
        public void CreateNewInterlayerUI()
        {
            using (FormNewInterlayer form = new FormNewInterlayer(this, null))
            {
                if (DialogResult.OK == form.ShowDialog())
                    CreateNewInterlayer(
                        form.ItemName, form.ItemDescription
                        , form.InterlayerLength, form.InterlayerWidth, form.Thickness
                        , form.Weight
                        , form.Color);
            }
        }
        /// <summary>
        /// Creates new pallet corners
        /// </summary>
        public void CreateNewPalletCornersUI()
        {
            using (FormNewPalletCorners form = new FormNewPalletCorners(this, null))
            {
                if (DialogResult.OK == form.ShowDialog())
                    CreateNewPalletCorners(form.ItemName, form.ItemDescription,
                        form.CornerLength, form.CornerWidth, form.CornerThickness,
                        form.CornerWeight,
                        form.CornerColor);
            }
        }
        /// <summary>
        /// Creates a new pallet cap
        /// </summary>
        public void CreateNewPalletCapUI()
        {
            using (FormNewPalletCap form = new FormNewPalletCap(this, null))
            {
                if (DialogResult.OK == form.ShowDialog())
                    CreateNewPalletCap(form.ItemName, form.ItemDescription,
                        form.CapLength, form.CapWidth, form.CapHeight,
                        form.CapInnerLength, form.CapInnerWidth, form.CapInnerHeight,
                        form.CapWeight, form.CapColor);
            }
        }
        /// <summary>
        /// Creates new pallet film
        /// </summary>
        public void CreateNewPalletFilmUI()
        {
            using (FormNewPalletFilm form = new FormNewPalletFilm(this, null))
            {
                if (DialogResult.OK == form.ShowDialog())
                    CreateNewPalletFilm(form.ItemName, form.ItemDescription,
                        form.UseTransparency,
                        form.UseHatching, form.HatchSpacing, form.HatchAngle,
                        form.FilmColor);
            }
        }
        /// <summary>
        /// creates a new PalletProperties object
        /// </summary>
        public void CreateNewPalletUI()
        {
            using (FormNewPallet form = new FormNewPallet(this, null))
            {
                if (DialogResult.OK == form.ShowDialog())
                    CreateNewPallet(form.ItemName, form.ItemDescription
                        , form.PalletTypeName
                        , form.PalletLength, form.PalletWidth, form.PalletHeight
                        , form.Weight
                        , form.PalletColor);
            }
        }
        /// <summary>
        /// Creates a new TruckProperties
        /// </summary>
        public void CreateNewTruckUI()
        {
            using (FormNewTruck form = new FormNewTruck(this, null))
            {
                if (DialogResult.OK == form.ShowDialog())
                    CreateNewTruck(form.ItemName, form.ItemDescription
                        , form.TruckLength, form.TruckWidth, form.TruckHeight
                        , form.TruckAdmissibleLoadWeight
                        , form.TruckColor);
            }
        }
        /// <summary>
        /// Creates a new palet analysis
        /// </summary>
        /// <returns>created palet analysis</returns>
        public void CreateNewAnalysisCasePalletUI()
        {
            if (!CanCreateAnalysisCasePallet && !CanCreateAnalysisBundlePallet) return;
            if (Properties.Settings.Default.DummyMode)
            {
                using (FormNewAnalysisCasePalletDM form = new FormNewAnalysisCasePalletDM(this, null))
                    if (DialogResult.OK == form.ShowDialog()) { } 
            }
            else
            {
                using (FormNewAnalysisCasePallet form = new FormNewAnalysisCasePallet(this, null))
                    if (DialogResult.OK == form.ShowDialog()) { } 
            }
        }
        public void CreateNewAnalysisBoxCaseUI()
        {
            if (!CanCreateAnalysisBoxCase && !CanCreateAnalysisBundleCase) return;
            using (FormNewAnalysisBoxCase form = new FormNewAnalysisBoxCase(this, null))
                if (DialogResult.OK == form.ShowDialog()) { } 
        }
        public void CreateNewAnalysisCylinderPalletUI()
        {
            if (!CanCreateAnalysisCylinderPallet) return;
            using (FormNewAnalysisCylinderPallet form = new FormNewAnalysisCylinderPallet(this, null))
                if (DialogResult.OK == form.ShowDialog()) { }
        }
        public void CreateNewAnalysisCylinderCaseUI()
        {
            if (!CanCreateAnalysisCylinderCase) return;
            using (FormNewAnalysisCylinderCase form = new FormNewAnalysisCylinderCase(this, null))
                if (DialogResult.OK == form.ShowDialog()) { }
        }
        public AnalysisPalletTruck CreateNewAnalysisPalletTruckUI()
        {
            if (!CanCreateAnalysisPalletTruck) return null;
            using (FormNewAnalysisPalletTruck form = new FormNewAnalysisPalletTruck(this, null))
                if (DialogResult.OK == form.ShowDialog()) { }
            return null;
        }
        public AnalysisCaseTruck CreateNewAnalysisCaseTruckUI()
        {
            if (!CanCreateAnalysisCaseTruck) return null;
            using (FormNewAnalysisCaseTruck form = new FormNewAnalysisCaseTruck(this, null))
                if (DialogResult.OK == form.ShowDialog()) { }
            return null;
        }
        public AnalysisCylinderTruck CreateNewAnalysisCylinderTruckUI()
        {
            if (!CanCreateAnalysisCylinderTruck) return null;
            using (FormNewAnalysisCylinderTruck form = new FormNewAnalysisCylinderTruck(this, null))
                if (DialogResult.OK == form.ShowDialog()) { }
            return null;
        }

        public void CreateNewHAnalysisPalletUI()
        {
            if (!CanCreateAnalysisCasePallet) return;
            using (FormNewHAnalysis form = new FormNewHAnalysisCasePallet(this, null))
                if (DialogResult.OK == form.ShowDialog()) { }
        }
        public void CreateNewHAnalysisTruckUI()
        {
            if (!CanCreateAnalysisCaseTruck) return;
            using (FormNewHAnalysis form = new FormNewHAnalysisCaseTruck(this, null))
                if (DialogResult.OK == form.ShowDialog()) { }
        }
        public void CreateNewAnalysisHCylPalletUI()
        {
            if (!CanCreateAnalysisCylinderPallet) return;
            using (var form = new FormNewAnalysisHCylPallet(this, null))
                if (DialogResult.OK == form.ShowDialog()) { }
        }
        public void ExportAnalysesToExcel()
        {
            // open excel file
            Microsoft.Office.Interop.Excel.Application xlApp = new Microsoft.Office.Interop.Excel.Application
            {
                Visible = true,
                DisplayAlerts = false
            };
            Workbooks xlWorkBooks = xlApp.Workbooks;
            Workbook xlWorkBook = xlWorkBooks.Add(Type.Missing);
            Worksheet xlWorkSheetCasePallet = xlWorkBook.Worksheets.get_Item(1);

            // create header
            xlWorkSheetCasePallet.Cells[1, 1] = "Analysis name";
            xlWorkSheetCasePallet.Cells[1, 2] = "Case name";
            xlWorkSheetCasePallet.Cells[1, 3] = "Case description";
            xlWorkSheetCasePallet.Cells[1, 4] = "Ext. length";
            xlWorkSheetCasePallet.Cells[1, 5] = "Ext. width";
            xlWorkSheetCasePallet.Cells[1, 6] = "Ext. height";
            xlWorkSheetCasePallet.Cells[1, 7] = "Max pallet height";
            xlWorkSheetCasePallet.Cells[1, 8] = "Solution case count";
            xlWorkSheetCasePallet.Cells[1, 9] = "Layers";
            xlWorkSheetCasePallet.Cells[1, 10] = "Cases per layer";
            xlWorkSheetCasePallet.Cells[1, 11] = "Load weight";
            xlWorkSheetCasePallet.Cells[1, 12] = "Weight";
            xlWorkSheetCasePallet.Cells[1, 13] = "Volume efficiency";
            xlWorkSheetCasePallet.Cells[1, 14] = "Image";

            Range headerRange = xlWorkSheetCasePallet.get_Range("A1", "N1");
            headerRange.Font.Bold = true;
            xlWorkSheetCasePallet.Columns.AutoFit();


            // *** get all users from Azure database and write them down
            int iRowCasePallet = 2;

            foreach (var analysis in Analyses)
            {
                try
                {
                    if (analysis is AnalysisCasePallet analysisCasePallet)
                    {
                        // analysis name
                        xlWorkSheetCasePallet.Cells[iRowCasePallet, 1] = analysisCasePallet.Name;
                        // case
                        BoxProperties caseProperties = analysisCasePallet.Content as BoxProperties;
                        xlWorkSheetCasePallet.Cells[iRowCasePallet, 2] = caseProperties.Name;
                        xlWorkSheetCasePallet.Cells[iRowCasePallet, 3] = caseProperties.Description;
                        xlWorkSheetCasePallet.Cells[iRowCasePallet, 4] = caseProperties.Length;
                        xlWorkSheetCasePallet.Cells[iRowCasePallet, 5] = caseProperties.Width;
                        xlWorkSheetCasePallet.Cells[iRowCasePallet, 6] = caseProperties.Height;
                        // constraints
                        ConstraintSetCasePallet constraintSet = analysisCasePallet.ConstraintSet as ConstraintSetCasePallet;
                        xlWorkSheetCasePallet.Cells[iRowCasePallet, 7] = constraintSet.OptMaxHeight.Value;
                        // solution
                        SolutionLayered sol = analysisCasePallet.SolutionLay;
                        xlWorkSheetCasePallet.Cells[iRowCasePallet, 8] = sol.ItemCount;
                        xlWorkSheetCasePallet.Cells[iRowCasePallet, 9] = sol.LayerCount;
                        xlWorkSheetCasePallet.Cells[iRowCasePallet, 10] = sol.LayerBoxCount(0);
                        xlWorkSheetCasePallet.Cells[iRowCasePallet, 11] = sol.LoadWeight;
                        xlWorkSheetCasePallet.Cells[iRowCasePallet, 12] = sol.Weight;
                        xlWorkSheetCasePallet.Cells[iRowCasePallet, 13] = sol.VolumeEfficiency;

                        var stackImagePath = Path.Combine(Path.ChangeExtension(Path.GetTempFileName(), "png"));
                        var graphics = new Graphics3DImage(new Size(768, 768))
                        {
                            FontSizeRatio = 0.01f,
                            CameraPosition = Graphics3D.Corner_0
                        };
                        using (ViewerSolution sv = new ViewerSolution(analysisCasePallet.SolutionLay))
                            sv.Draw(graphics, Transform3D.Identity);
                        graphics.Flush();
                        Bitmap bmp = graphics.Bitmap;
                        bmp.Save(stackImagePath, System.Drawing.Imaging.ImageFormat.Png);
                        Range imageCell = (Range)xlWorkSheetCasePallet.Cells[iRowCasePallet, 14];
                        imageCell.RowHeight = 128;
                        imageCell.ColumnWidth = 24;
                        xlWorkSheetCasePallet.Shapes.AddPicture(stackImagePath,
                            LinkToFile: MsoTriState.msoFalse, SaveWithDocument: MsoTriState.msoCTrue,
                            Left: imageCell.Left + 1, Top: imageCell.Top + 1, Width: imageCell.Width - 2, Height: imageCell.Height - 2);

                        ++iRowCasePallet;
                    }
                    else if (analysis is AnalysisBoxCase analysisBoxCase)
                    {
                    }
                    else if (analysis is AnalysisCaseTruck analysisCaseTruck)
                    {
                    }
                    else if (analysis is AnalysisCylinderPallet analysisCylinderPallet)
                    {
                    }
                    else if (analysis is AnalysisCylinderCase analysisCylinderCase)
                    {
                    }
                    else if (analysis is AnalysisCylinderTruck analysisCylinderTruck)
                    {
                    }
                }
                catch (Exception ex)
                {
                    _log.Error(ex.ToString());
                }
            }
        }
        #endregion

        #region UI item edition
        public void EditAnalysis(AnalysisLayered analysis)
        {
            // search for any DockContentAnalysis window and close it
            var seq = (from view in Views
                       where view is DockContentAnalysisEdit && analysis == (view as DockContentAnalysisEdit).Analysis
                       select view);
            if (seq.Count() > 0) seq.First().Close();

            // instantiate a form to edit analysis
            Form form = null;
            if (analysis is AnalysisCasePallet) form = new FormNewAnalysisCasePallet(this, analysis);
            else if (analysis is AnalysisBoxCase) form = new FormNewAnalysisBoxCase(this, analysis);
            else if (analysis is AnalysisCylinderPallet) form = new FormNewAnalysisCylinderPallet(this, analysis);
            else if (analysis is AnalysisCylinderCase) form = new FormNewAnalysisCylinderCase(this, analysis);
            else if (analysis is AnalysisPalletTruck) form = new FormNewAnalysisPalletTruck(this, analysis);
            else if (analysis is AnalysisCaseTruck) form = new FormNewAnalysisCaseTruck(this, analysis);
            else if (analysis is AnalysisCylinderTruck) form = new FormNewAnalysisCylinderTruck(this, analysis);
            else
            {
                MessageBox.Show("Unexepected analysis type!");
                return;
            }
            if (DialogResult.OK == form.ShowDialog()) { }
        }
        public void EditAnalysis(AnalysisHetero analysis)
        {
            // search for DockContentHAnalysis window and close it
            var seq = (from view in Views
                       where view is DockContentHAnalysisCasePallet && (analysis == (view as DockContentHAnalysisCasePallet).Analysis)
                       select view);
            if (seq.Count() > 0) seq.First().Close();

            // instantiate a form to edit analysis
            Form form = null;
            if (analysis is HAnalysisPallet) form = new FormNewHAnalysisCasePallet(this, analysis);
            else if (analysis is HAnalysisCase) form = new FormNewHAnalysisBoxCase(this, analysis);
            else if (analysis is HAnalysisTruck) form = new FormNewHAnalysisCaseTruck(this, analysis);
            else
            {
                _log.Error($"Unexpected analysis type = {analysis.GetType().ToString()}");
                return;
            }
            if (DialogResult.OK == form.ShowDialog()) { }
        }
        #endregion

        #region Object override
        public override string ToString()
        {
            return Name;
        }
        #endregion
    }
}
