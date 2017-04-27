﻿#region Using directives
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Globalization;

using log4net;
using Sharp3D.Math.Core;

using treeDiM.StackBuilder.Basics;
using treeDiM.StackBuilder.Graphics;
using treeDiM.StackBuilder.Graphics.Controls;
using treeDiM.StackBuilder.Engine;
using treeDiM.PLMPack.DBClient;
using treeDiM.PLMPack.DBClient.PLMPackSR;
using treeDiM.StackBuilder.Desktop.Properties;
#endregion

namespace treeDiM.StackBuilder.Desktop
{
    public partial class FormOptimiseMultiCase : Form, IItemBaseFilter, IDrawingContainer
    {
        #region Constructor
        public FormOptimiseMultiCase()
        {
            InitializeComponent();
        }
        #endregion

        #region Form override
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            // fill combo boxes
            cbBoxes.Initialize(_doc, this, null);
            // initialize orientation
            uCtrlCaseOrient.AllowedOrientations = new bool[] { Settings.Default.AllowVerticalX, Settings.Default.AllowVerticalY, Settings.Default.AllowVerticalZ };
            // initialize grid
            gridSolutions.Selection.SelectionChanged += new SourceGrid.RangeRegionChangedEventHandler(onSelChangeGrid);
            // load cases
            DCSBCase[] sbCases = WCFClientSingleton.Instance.Client.GetAllCases();
            foreach (DCSBCase sbCase in sbCases)
            {
                // get values from database
                if (!sbCase.HasInnerDims) return;
                UnitsManager.UnitSystem us = (UnitsManager.UnitSystem)sbCase.UnitSystem;
                double boxLength = UnitsManager.ConvertLengthFrom(sbCase.DimensionsOuter.M0, us);
                double boxWidth = UnitsManager.ConvertLengthFrom(sbCase.DimensionsOuter.M1, us);
                double boxHeight = UnitsManager.ConvertLengthFrom(sbCase.DimensionsOuter.M2, us);
                double innerLength = UnitsManager.ConvertLengthFrom(sbCase.DimensionsInner.M0, us);
                double innerWidth = UnitsManager.ConvertLengthFrom(sbCase.DimensionsInner.M1, us);
                double innerHeight = UnitsManager.ConvertLengthFrom(sbCase.DimensionsInner.M2, us);
                double weight = UnitsManager.ConvertMassFrom(sbCase.Weight, us);
                Color[] colors = new Color[6];
                for (int i = 0; i < 6; ++i)
                    colors[i] = Color.FromArgb(sbCase.Colors[i]);

                // instantiate box properties
                BoxProperties bProperties = new BoxProperties(null
                    , boxLength, boxWidth, boxHeight
                    , innerLength, innerWidth, innerHeight);
                bProperties.ID.SetNameDesc(sbCase.Name, sbCase.Description);
                bProperties.SetWeight(weight);
                bProperties.TapeWidth = new OptDouble(sbCase.ShowTape, sbCase.TapeWidth);
                bProperties.TapeColor = Color.FromArgb(sbCase.TapeColor);
                bProperties.SetAllColors(colors);
                // insert in listbox control
                chklbCases.Items.Add( new ItemBaseCB(bProperties), true );
                // graph control
                graphCtrl.DrawingContainer = this;
            }
        }
        #endregion

        #region Status toolstrip updating
        public virtual void UpdateStatus(string message)
        {
            // status + message
            if (!uCtrlCaseOrient.HasOrientationSelected)
                message = Resources.ID_NOORIENTATIONSELECTED;
            else if (_checkedIndices.Count < 1)
                message = Resources.ID_NOCASESELECTED;
            else if (null == SelectedAnalysis)
                message = Resources.ID_ANALYSISHASNOVALIDSOLUTION;
            else if (string.IsNullOrEmpty(AnalysisName))
                message = Resources.ID_FIELDNAMEEMPTY;
            else if (string.IsNullOrEmpty(AnalysisDescription))
                message = Resources.ID_FIELDDESCRIPTIONEMPTY;

            toolStripStatusLabelDef.ForeColor = string.IsNullOrEmpty(message) ? Color.Black : Color.Red;
            toolStripStatusLabelDef.Text = string.IsNullOrEmpty(message) ? Resources.ID_READY : message;

            bnCreateAnalysis.Enabled = string.IsNullOrEmpty(message);
        }
        #endregion

        #region IItemBaseFilter
        public bool Accept(Control ctrl, ItemBase itemBase)
        {
            if (ctrl == cbBoxes)
            {
                PackableBrick packable = itemBase as PackableBrick;
                if (null == packable)
                    return false;
                BoxProperties bProperties = packable as BoxProperties;
                if (null != bProperties)
                    return !bProperties.HasInsideDimensions;
                return true;
            }
            return false;
        }
        #endregion

        #region IDrawingContainer
        public void Draw(Graphics3DControl ctrl, Graphics3D graphics)
        {
            if (null == SelectedAnalysis)
                return;
            try
            {
                bool showDimensions = true;
                ViewerSolution sv = new ViewerSolution(SelectedAnalysis.Solution);
                sv.Draw(graphics, Transform3D.Identity, showDimensions);
            }
            catch (Exception ex)
            {
                _log.Error(ex.Message);
            }
        }
        #endregion

        #region Event handlers
        private void onBoxChanged(object sender, EventArgs e)
        {
            if (0 == cbBoxes.Items.Count) return;
            PackableBrick packable = cbBoxes.SelectedType as PackableBrick;
            if (null != packable)
                uCtrlCaseOrient.BProperties = packable;
            ComputeSolutions();
            UpdateStatus(string.Empty);
        }
        private void onOrientationChanged(object sender, EventArgs e)
        {
            ComputeSolutions();
            UpdateStatus(string.Empty);
        }
        private void onCaseChecked(object sender, ItemCheckEventArgs e)
        {
            // build list of checked items in chklbCases
            _checkedIndices.Clear();
            foreach (int index in chklbCases.CheckedIndices)
                _checkedIndices.Add(index);
            if (e.NewValue == CheckState.Checked)
                _checkedIndices.Add(e.Index);
            else if (e.NewValue == CheckState.Unchecked)
                _checkedIndices.Remove(e.Index);

            ComputeSolutions();
            UpdateStatus(string.Empty);
        }
        private void onCreateNewCaseAnalysis(object sender, EventArgs e)
        {
            try
            {
                // selected analysis
                AnalysisBoxCase analysisSel = SelectedAnalysis;
                BoxProperties caseSel = analysisSel.CaseProperties;
                // create case
                BoxProperties caseProperties = _doc.CreateNewBox(caseSel);
                List<InterlayerProperties> interlayers = new List<InterlayerProperties>();
                Analysis analysis = _doc.CreateNewAnalysisBoxCase(
                    AnalysisName, AnalysisDescription,
                    analysisSel.Content, caseProperties,
                    interlayers,
                    analysisSel.ConstraintSet as ConstraintSetBoxCase,
                    analysisSel.Solution.LayerDescriptors);
            }
            catch (Exception ex)
            {
                _log.Error(ex.Message);
            }
        }
        private void onSelChangeGrid(object sender, SourceGrid.RangeRegionChangedEventArgs e)
        {
            try
            {
                SourceGrid.Selection.RowSelection select = sender as SourceGrid.Selection.RowSelection;
                SourceGrid.Grid g = select.Grid as SourceGrid.Grid;

                SourceGrid.RangeRegion region = g.Selection.GetSelectionRegion();
                int[] indexes = region.GetRowsIndex();
                if (indexes.Length < 1 || indexes[0] < 1)
                    _selectedAnalysis = null;
                else
                {
                    _selectedAnalysis = _analyses[indexes[0] - 1];
                    // update drawing
                    graphCtrl.Invalidate();
                    // analysis name/description
                    if (null != _selectedAnalysis)
                    {
                        AnalysisName = string.Format("Analysis_{0}_in_{1}", _selectedAnalysis.Content.ID.Name, _selectedAnalysis.Container.ID.Name);
                        AnalysisDescription = string.Format(" Packing {0} in {1}", _selectedAnalysis.Content.Name, _selectedAnalysis.Container.Name);
                    }
                    else
                    {
                        AnalysisName = string.Empty;
                        AnalysisDescription = string.Empty;
                    }
                }
                graphCtrl.Invalidate();
            }
            catch (Exception ex)
            {
                _log.Error(ex.Message);
            }
        }
        #endregion

        #region Compute solutions
        protected void ComputeSolutions()
        {
            // sanity checks 
            if (cbBoxes.Items.Count == 0 || chklbCases.Items.Count == 0)
                return;
            // clear existing analyses
            _analyses.Clear();

            try
            {
                PackableBrick packable = cbBoxes.SelectedType as PackableBrick;

                // build list of analyses
                for (int i = 0; i < _checkedIndices.Count; ++i)
                {
                    BoxProperties caseProperties = (chklbCases.Items[_checkedIndices[i]] as ItemBaseCB).Item as BoxProperties;
                    if (null != caseProperties)
                    {
                        // build constraint set
                        ConstraintSetBoxCase constraintSet = new ConstraintSetBoxCase(caseProperties);
                        constraintSet.SetAllowedOrientations(uCtrlCaseOrient.AllowedOrientations);
                        // build solver + get analyses
                        SolverBoxCase solver = new SolverBoxCase(packable, caseProperties);
                        _analyses.AddRange(solver.BuildAnalyses(constraintSet));
                    }
                }
                // sort analysis
                _analyses.Sort(new ComparerAnalysisBoxCase());
                // fill grid
                FillGrid();
            }
            catch (Exception ex)
            {
                _log.Error(ex.Message);
            }
        }
        #endregion

        #region Grid
        private void FillGrid()
        {
            // remove all existing rows
            gridSolutions.Rows.Clear();
            // *** IViews 
            // captionHeader
            SourceGrid.Cells.Views.RowHeader captionHeader = new SourceGrid.Cells.Views.RowHeader();
            DevAge.Drawing.VisualElements.RowHeader veHeaderCaption = new DevAge.Drawing.VisualElements.RowHeader();
            veHeaderCaption.BackColor = Color.SteelBlue;
            veHeaderCaption.Border = DevAge.Drawing.RectangleBorder.NoBorder;
            captionHeader.Background = veHeaderCaption;
            captionHeader.ForeColor = Color.Black;
            captionHeader.Font = new Font("Arial", 10, FontStyle.Bold);
            captionHeader.TextAlignment = DevAge.Drawing.ContentAlignment.MiddleCenter;
            // viewRowHeader
            SourceGrid.Cells.Views.ColumnHeader viewColumnHeader = new SourceGrid.Cells.Views.ColumnHeader();
            DevAge.Drawing.VisualElements.ColumnHeader backHeader = new DevAge.Drawing.VisualElements.ColumnHeader();
            backHeader.BackColor = Color.LightGray;
            backHeader.Border = DevAge.Drawing.RectangleBorder.NoBorder;
            viewColumnHeader.Background = backHeader;
            viewColumnHeader.ForeColor = Color.Black;
            viewColumnHeader.Font = new Font("Arial", 10, FontStyle.Regular);
            viewColumnHeader.ElementSort.SortStyle = DevAge.Drawing.HeaderSortStyle.None;
            // viewNormal
            CellBackColorAlternate viewNormal = new CellBackColorAlternate(Color.LightBlue, Color.White);
            // ***
            // set first row
            gridSolutions.BorderStyle = BorderStyle.FixedSingle;
            gridSolutions.ColumnsCount = 5;
            gridSolutions.FixedRows = 1;
            gridSolutions.Rows.Insert(0);
            // header
            int iCol = 0;
            SourceGrid.Cells.ColumnHeader columnHeader;
            // name
            columnHeader = new SourceGrid.Cells.ColumnHeader(Properties.Resources.ID_CASENAME);
            columnHeader.AutomaticSortEnabled = false;
            columnHeader.View = viewColumnHeader;
            gridSolutions[0, iCol++] = columnHeader;
            // dimensions
            columnHeader = new SourceGrid.Cells.ColumnHeader(string.Format(Properties.Resources.ID_DIMENSIONS, UnitsManager.LengthUnitString));
            columnHeader.AutomaticSortEnabled = false;
            columnHeader.View = viewColumnHeader;
            gridSolutions[0, iCol++] = columnHeader;
            // #items
            columnHeader = new SourceGrid.Cells.ColumnHeader("#");
            columnHeader.AutomaticSortEnabled = false;
            columnHeader.View = viewColumnHeader;
            gridSolutions[0, iCol++] = columnHeader;
            // efficiency
            columnHeader = new SourceGrid.Cells.ColumnHeader(Properties.Resources.ID_EFFICIENCYPERCENTAGE);
            columnHeader.AutomaticSortEnabled = false;
            columnHeader.View = viewColumnHeader;
            gridSolutions[0, iCol++] = columnHeader;
            // weight
            columnHeader = new SourceGrid.Cells.ColumnHeader(string.Format(Properties.Resources.ID_WEIGHT, UnitsManager.MassUnitString));
            columnHeader.AutomaticSortEnabled = false;
            columnHeader.View = viewColumnHeader;
            gridSolutions[0, iCol++] = columnHeader;

            int iRow = 0;
            foreach (Analysis analysis in _analyses)
            {
                AnalysisBoxCase analysisBoxCase = analysis as AnalysisBoxCase;
                BoxProperties caseProperties = analysisBoxCase.CaseProperties;

                gridSolutions.Rows.Insert(++iRow);
                iCol = 0;
                gridSolutions[iRow, iCol++] = new SourceGrid.Cells.Cell(analysis.Container.ID.Name);
                gridSolutions[iRow, iCol++] = new SourceGrid.Cells.Cell(string.Format("{0} x {1} x {2} / {3} x {4} x {5}",
                    caseProperties.Length, caseProperties.Width, caseProperties.Height,
                    caseProperties.InsideLength, caseProperties.InsideWidth, caseProperties.InsideHeight));
                gridSolutions[iRow, iCol++] = new SourceGrid.Cells.Cell(analysis.Solution.ItemCount);
                gridSolutions[iRow, iCol++] = new SourceGrid.Cells.Cell(string.Format(CultureInfo.InvariantCulture, "{0:0.#}", analysis.Solution.VolumeEfficiency));
                gridSolutions[iRow, iCol++] = new SourceGrid.Cells.Cell(string.Format(CultureInfo.InvariantCulture, "{0:0.#}", analysis.Solution.Weight));
            }

            gridSolutions.AutoStretchColumnsToFitWidth = true;
            gridSolutions.AutoSizeCells();
            gridSolutions.Columns.StretchToFit();

            // select first solution
            if (gridSolutions.RowsCount > 1)
                gridSolutions.Selection.SelectRow(1, true);
            else
            {
                // grid empty -> clear drawing
                _selectedAnalysis = null;
                graphCtrl.Invalidate();
            }
        }
        private AnalysisBoxCase SelectedAnalysis
        {
            get { return _selectedAnalysis as AnalysisBoxCase; }
        }
        #endregion

        #region Public properties
        public DocumentSB Document
        {
            get { return _doc; }
            set { _doc = value; }
        }
        private string AnalysisName
        {
            get { return tbAnalysisName.Text; }
            set { tbAnalysisName.Text = value; }
        }
        private string AnalysisDescription
        {
            get { return tbAnalysisDescription.Text; }
            set { tbAnalysisDescription.Text = value; }
        }
        #endregion

        #region Data members
        private DocumentSB _doc;
        private List<Analysis> _analyses = new List<Analysis>();
        private List<int> _checkedIndices = new List<int>();
        private Analysis _selectedAnalysis;
        protected static ILog _log = LogManager.GetLogger(typeof(FormOptimiseMultiCase));
        #endregion
    }
}
