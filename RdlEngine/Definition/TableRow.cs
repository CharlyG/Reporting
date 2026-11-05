

using System;
using System.Xml;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using Majorsilence.Reporting.RdlEngine.Resources;
using System.Threading.Tasks;

#if DRAWINGCOMPAT
using Majorsilence.Forms.Drawing;
using System.Drawing;  // value types (Color, Point, Size, Rectangle, ...) come from System.Drawing.Primitives
#else
using System.Drawing;
#endif

namespace Majorsilence.Reporting.Rdl
{
	///<summary>
	/// TableRow represents a Row in a table.  This can be part of a header, footer, or detail definition.
	///</summary>
	[Serializable]
	internal class TableRow : ReportLink
	{
		TableCells _TableCells;	// Contents of the row. One cell per column
		RSize _Height;				// Height of the row
		Visibility _Visibility;		// Indicates if the row should be hidden		
		bool _CanGrow;			// indicates that row height can increase in size
		bool _CanShrink;		// indicates that row height can decrease in size
		List<Textbox> _GrowList;	// list of TextBox's that need to be checked for growth
		float _FitHeight;		// CrystalEngine: the height this row is fitted to a page by, when less than its own
		bool _PageBreakBefore;	// CrystalEngine: Crystal's New Page Before on a section - this row starts a page
		float _KeepWithNext;		// CrystalEngine: this row stays on the same page as the row after it; the height in points the run is held to

		internal TableRow(ReportDefn r, ReportLink p, XmlNode xNode) : base(r, p)
		{
			_TableCells=null;
			_Height=null;
			_Visibility=null;
			_CanGrow = false;
			_CanShrink = false;
			_GrowList = null;

			// Loop thru all the child nodes
			foreach(XmlNode xNodeLoop in xNode.ChildNodes)
			{
				if (xNodeLoop.NodeType != XmlNodeType.Element)
					continue;
				switch (xNodeLoop.Name)
				{
					case "TableCells":
						_TableCells = new TableCells(r, this, xNodeLoop);
						break;
					case "Height":
						_Height = new RSize(r, xNodeLoop);
						break;
					case "Visibility":
						_Visibility = new Visibility(r, this, xNodeLoop);
						break;
					case "FitHeight":
						{
							float fh;
							if (float.TryParse(xNodeLoop.InnerText.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out fh))
								_FitHeight = fh;
						}
						break;
					case "PageBreakBefore":
						_PageBreakBefore = xNodeLoop.InnerText.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
						break;
					case "KeepWithNext":
						{
							string kwn = xNodeLoop.InnerText.Trim();
							float kh;
							_KeepWithNext = kwn.Equals("true", StringComparison.OrdinalIgnoreCase) ? float.MaxValue
								: float.TryParse(kwn, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out kh) ? kh : 0;
						}
						break;
					default:
						// don't know this element - log it
						OwnerReport.rl.LogError(4, "Unknown TableRow element '" + xNodeLoop.Name + "' ignored.");
						break;
				}
			}
			if (_TableCells == null)
				OwnerReport.rl.LogError(8, "TableRow requires the TableCells element.");
			if (_Height == null)
				OwnerReport.rl.LogError(8, "TableRow requires the Height element.");
		}
		
		async override internal Task FinalPass()
		{
            await _TableCells.FinalPass();
			if (_Visibility != null)
                await _Visibility.FinalPass();

			foreach (TableCell tc in _TableCells.Items)
			{
				ReportItem ri = tc.ReportItems.Items[0] as ReportItem;
				if (!(ri is Textbox))
					continue;
				Textbox tb = ri as Textbox;
				if (tb.CanGrow)
				{
					if (this._GrowList == null)
						_GrowList = new List<Textbox>();
					_GrowList.Add(tb);
					_CanGrow = true;
				}
				if (tb.CanShrink)
					_CanShrink = true;
			}

			if (_CanGrow)				// shrink down the resulting list
                _GrowList.TrimExcess();

			return;
		}

		internal async Task Run(IPresent ip, Row row)
		{
			if (this.Visibility != null && await Visibility.IsHidden(ip.Report(), row))
				return;

            await ip.TableRowStart(this, row);
            await _TableCells.Run(ip, row);
			ip.TableRowEnd(this, row);
			return ;
		}
 
		internal async Task RunPage(Pages pgs, Row row)
		{
			if (this.Visibility != null && await Visibility.IsHidden(pgs.Report, row))
				return;

			Page startPage = pgs.CurrentPage;
			float startY = startPage.YOffset;
            await _TableCells.RunPage(pgs, row);

			WorkClass wc = GetWC(pgs.Report);
			// CrystalEngine: a subreport in a cell can run past the row's height; the row is as tall as it ran.
			// TableCells leaves the offset where the tallest subreport ended, or at the row start.
			if (pgs.CurrentPage == startPage)
				startPage.YOffset = startY + Math.Max(wc.CalcHeight, startPage.YOffset - startY);
			else if (!HasSubreport)
				pgs.CurrentPage.YOffset += wc.CalcHeight;	// (a subreport that broke the page has already advanced it)
			return ;
		}

		internal TableCells TableCells
		{
			get { return  _TableCells; }
			set {  _TableCells = value; }
		}

		internal bool HasSubreport
		{
			get
			{
				foreach (TableCell tc in _TableCells.Items)
					if (tc.ReportItems.Items[0] is Subreport)
						return true;
				return false;
			}
		}

		internal float FitHeight
		{
			get { return _FitHeight; }
		}

		internal bool PageBreakBefore
		{
			get { return _PageBreakBefore; }
		}

		internal float KeepWithNext
		{
			get { return _KeepWithNext; }
		}

		internal RSize Height
		{
			get { return  _Height; }
			set {  _Height = value; }
		}
        internal async Task<float> HeightOfRow(Pages pgs, Row r)
        {
            return await HeightOfRow(pgs.Report, pgs.G, r);
        }
		internal async Task<float> HeightOfRow(Report rpt, Graphics g, Row r)
		{
			WorkClass wc = GetWC(rpt);
			if (this.Visibility != null && await Visibility.IsHidden(rpt, r))
			{
				wc.CalcHeight = 0;
				return 0;
			}

			float defnHeight = _Height.Points;
			if (!_CanGrow && !_CanShrink)
			{
				wc.CalcHeight = defnHeight;
				return defnHeight;
			}

			// Determine the row height from the natural (content) height of each cell.
			// A fixed cell always needs the defined height; a CanGrow cell may exceed
			// it; a CanShrink cell may fall below it.  The row takes the tallest
			// requirement of its cells.
			TableColumns tcs = this.Table.TableColumns;
			float height = 0;
			foreach (TableCell tc in _TableCells.Items)
			{
				Textbox tb = tc.ReportItems.Items[0] as Textbox;
				float cellHeight;
				if (tb != null && (tb.CanGrow || tb.CanShrink))
				{
					int ci = tb.TC.ColIndex;
					if (await tcs[ci].IsHidden(rpt, r))    // if column is hidden don't use in calculation
						continue;
					float content = await tb.RunTextCalcHeight(rpt, g, r);
					if (tb.CanGrow && !tb.CanShrink)
						cellHeight = Math.Max(content, defnHeight);  // grow only
					else
						cellHeight = content;                        // shrink-only or grow+shrink
				}
				else
					cellHeight = defnHeight;                         // fixed item needs full height
				height = Math.Max(height, cellHeight);
			}

			// Preserve prior behaviour: a CanGrow row never drops below its defined
			// height.  A CanShrink-only row may shrink but never exceed it.
			if (_CanGrow)
				height = Math.Max(height, defnHeight);
			else
				height = Math.Min(height, defnHeight);

			wc.CalcHeight = height;
			return wc.CalcHeight;
		}

		internal float HeightCalc(Report rpt)
		{
			WorkClass wc = GetWC(rpt);
			return wc.CalcHeight;
		}

        private Table Table
        {
            get
            {
                ReportLink p= this.Parent;
                while (p != null)
                {
                    if (p is Table)
                        return p as Table;
                    p = p.Parent;
                }
                throw new Exception(Strings.TableRow_Error_TableRowNotRelatedToTable);
            }
        }

            internal Visibility Visibility
		{
			get { return  _Visibility; }
			set {  _Visibility = value; }
		}

		internal bool CanGrow
		{
			get { return _CanGrow; }
		}

		internal bool CanShrink
		{
			get { return _CanShrink; }
		}

		internal List<Textbox> GrowList
		{
			get { return _GrowList; }
		}

		private WorkClass GetWC(Report rpt)
		{
			WorkClass wc = rpt.Cache.Get(this, "wc") as WorkClass;
			if (wc == null)
			{
				wc = new WorkClass(this);
				rpt.Cache.Add(this, "wc", wc);
			}
			return wc;
		}

		private void RemoveWC(Report rpt)
		{
			rpt.Cache.Remove(this, "wc");
		}

		class WorkClass
		{
			internal float CalcHeight;		// dynamic when CanGrow true
			internal WorkClass(TableRow tr)
			{
				CalcHeight = tr.Height.Points;
			}
		}
	}
}
