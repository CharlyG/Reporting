
using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml;
using System.IO;
using Majorsilence.Reporting.RdlEngine.Resources;
using System.Threading.Tasks;

namespace Majorsilence.Reporting.Rdl
{
	///<summary>
	/// TableRows definition and processing.
	///</summary>
	[Serializable]
	internal class TableRows : ReportLink
	{
        List<TableRow> _Items;			// list of TableRow
		float _HeightOfRows;		// height of contained rows
		bool _CanGrow;				// if any TableRow contains a TextBox with CanGrow
		bool _CanShrink;			// if any TableRow contains a TextBox with CanShrink

		internal TableRows(ReportDefn r, ReportLink p, XmlNode xNode) : base(r, p)
		{
			TableRow t;
            _Items = new List<TableRow>();
			_CanGrow = false;
			_CanShrink = false;
			// Loop thru all the child nodes
			foreach(XmlNode xNodeLoop in xNode.ChildNodes)
			{
				if (xNodeLoop.NodeType != XmlNodeType.Element)
					continue;
				switch (xNodeLoop.Name)
				{
					case "TableRow":
						t = new TableRow(r, this, xNodeLoop);
						break;
					default:	
						t=null;		// don't know what this is
						// don't know this element - log it
						OwnerReport.rl.LogError(4, "Unknown TableRows element '" + xNodeLoop.Name + "' ignored.");
						break;
				}
				if (t != null)
					_Items.Add(t);
			}
			if (_Items.Count == 0)
				OwnerReport.rl.LogError(8, "For TableRows at least one TableRow is required.");
			else
                _Items.TrimExcess();
		}
		
		async override internal Task FinalPass()
		{
			_HeightOfRows = 0;
			foreach (TableRow t in _Items)
			{
				_HeightOfRows += t.Height.Points;
                await t.FinalPass();
				_CanGrow |= t.CanGrow;
				_CanShrink |= t.CanShrink;
			}

			return;
		}

		internal async Task Run(IPresent ip, Row row)
		{
			foreach (TableRow t in _Items)
			{
                await t.Run(ip, row);
			}
			return;
		}

		internal async Task RunPage(Pages pgs, Row row)
		{
            await RunPage(pgs, row, false);
		}

		// CrystalEngine: a row holding a subreport prints as tall as the subreport, which the designed heights do not count
		internal bool HasSubreport
		{
			get { return _Items.Exists(t => t.HasSubreport); }
		}

		// CrystalEngine: the height a row prints at, found by running it once on a scratch page with room for all of it
		async Task<float> MeasureRow(Pages pgs, Row row, TableRow t)
		{
			// not disposed: that would dispose the report it shares
			Pages scratch = new Pages(pgs.Report);
			Page sp = new Page(pgs.CurrentPage.PageNumber);
			scratch.AddPage(sp);
			scratch.BottomOfPage = 10000000f;
			sp.YOffset = 0;
			if (t.Visibility != null && await t.Visibility.IsHidden(pgs.Report, row))
				return 0;
			// the report's page number is the scratch run's while it runs; the real page's after it
			int pageNumber = pgs.Report.PageNumber;
			try { await t.TableCells.RunPage(scratch, row); }	// the cells only: the row's own height is the designed one
			finally { pgs.Report.PageNumber = pageNumber; }
			return scratch.LastPage.YOffset;
		}

		// CrystalEngine: a row of a section ticked New Page Before starts a page, unless it is hidden or the page is still empty
		async Task BreakBefore(Pages pgs, Row row, TableRow t)
		{
			if (!t.PageBreakBefore || pgs.CurrentPage.IsEmpty())
				return;
			if (t.Visibility != null && await t.Visibility.IsHidden(pgs.Report, row))
				return;
			Page p = OwnerTable.RunPageNew(pgs, pgs.CurrentPage);
			await OwnerTable.RunPageHeader(pgs, row, false, null);
		}

		// CrystalEngine: a row of a section ticked Print at Bottom of Page ends at the foot of the page
		async Task AlignBottom(Pages pgs, Row row, TableRow t)
		{
			if (!t.PrintAtBottom)
				return;
			if (t.Visibility != null && await t.Visibility.IsHidden(pgs.Report, row))
				return;
			Page p = pgs.CurrentPage;
			float top = pgs.BottomOfPage - await t.HeightOfRow(pgs, row);
			if (top > p.YOffset)
				p.YOffset = top;
		}

		internal async Task RunPage(Pages pgs, Row row, bool bCheckRows)
		{
			if (bCheckRows)
			{	// we need to check to see if a row will fit on the page
				float carry = 0;
				for (int i = 0; i < _Items.Count; i++)
				{
					TableRow t = _Items[i];
					await BreakBefore(pgs, row, t);
					Page p = pgs.CurrentPage;			// this can change after running a row
					float hrows = await t.HeightOfRow(pgs, row);	// height of this row
					// CrystalEngine: a row flagged KeepWithNext moves to the next page together with the rows
					// after it, up to and including the first row that is not flagged, when the run does not
					// fit as a whole. A run taller than half the page is left to split.
					float run = hrows;
					for (int j = i; j < _Items.Count - 1 && _Items[j].KeepWithNext > 0; j++)
						run += await _Items[j + 1].HeightOfRow(pgs, row);
					// Held to the height the section was designed at, which the file gives.
						if (t.KeepWithNext > 0 && run > t.KeepWithNext)
							run = Math.Max(hrows, t.KeepWithNext);
						bool keepRun = run > hrows && run < pgs.BottomOfPage / 2;
					float fit = t.FitHeight > 0 ? Math.Min(hrows, t.FitHeight) : hrows;
					// CrystalEngine: a subreport starts where it is and breaks across pages in its own tables
					if (t.KeepSubreport)
						{
							// moved whole to the next page when what it prints fits a fresh page and less than half the page
							// is left; otherwise it starts where it is and breaks in its own tables. IDT's Signatures (608pt
							// with 334pt left) move as Crystal's do, its Orders (514pt with 478pt left) start where they are
							// and break, and OAAsmtE's wounds (1118pt) are taller than a page
							float printed = await MeasureRow(pgs, row, t);
							bool fitsPage = printed <= pgs.BottomOfPage - OwnerReport.TopOfPage;
							bool lowerHalf = pgs.BottomOfPage - p.YOffset < pgs.BottomOfPage / 2;
							fit = fitsPage && lowerHalf ? printed : Math.Min(fit, 60);
						}
					else if (t.HasSubreport)
						fit = Math.Min(fit, 60);
						float height = p.YOffset + (keepRun ? run : fit);
					if (height > pgs.BottomOfPage)
					{
						p = OwnerTable.RunPageNew(pgs, p);
						await OwnerTable.RunPageHeader(pgs, row, false, null);
						// CrystalEngine: the part of the last row, fitted by where its objects end, that ran
						// past the foot of the page is the first thing on this one.
						p.YOffset += carry;
					}
					carry = 0;
					await AlignBottom(pgs, row, t);
					await t.RunPage(pgs, row);
					if (fit < hrows && pgs.CurrentPage.YOffset > pgs.BottomOfPage)
						carry = pgs.CurrentPage.YOffset - pgs.BottomOfPage;
				}
			}
			else
			{	// all rows will fit on the page
				foreach (TableRow t in _Items)
				{
					await BreakBefore(pgs, row, t);
					await AlignBottom(pgs, row, t);
                    await t.RunPage(pgs, row);
				}
			}
			return;
		}

		internal Table OwnerTable
		{
			get 
			{
				for (ReportLink rl = this.Parent; rl != null; rl = rl.Parent)
				{
					if (rl is Table)
						return rl as Table;
				}

				throw new Exception(Strings.TableRows_Error_TableRowsMustOwnedTable);
			}
		}

		internal float DefnHeight()
		{
			float height=0;
			foreach (TableRow tr in this._Items)
			{
				height += tr.Height.Points;
			}
			return height;
		}

		internal async Task<float> HeightOfRows(Pages pgs, Row r)
		{
			if (!this._CanGrow && !this._CanShrink)
				return _HeightOfRows;

			float height=0;
			foreach (TableRow tr in this._Items)
			{
				height += await tr.HeightOfRow(pgs, r);
			}

			// When rows can shrink, the total may legitimately fall below the sum of
			// defined heights, so the defined-height floor must not be applied.
			if (this._CanShrink)
				return height;

			return Math.Max(height, _HeightOfRows);
		}

        internal List<TableRow> Items
		{
			get { return  _Items; }
		}
	}
}
