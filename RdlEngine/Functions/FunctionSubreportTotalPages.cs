using System;
using System.Threading.Tasks;

namespace Majorsilence.Reporting.Rdl
{
	/// <summary>
	/// CrystalEngine: the number of pages the running subreport prints on, as Crystal counts a subreport's
	/// Total Page Count. It is not known while the subreport runs, so the text it is part of carries
	/// Report.SubreportTotalPagesMark and the subreport puts the count in its place when it has finished
	/// (Subreport.FillTotalPages). Outside a subreport it is the report's total.
	/// </summary>
	[Serializable]
	internal class FunctionSubreportTotalPages : IExpr
	{
		public FunctionSubreportTotalPages()
		{
		}

		public TypeCode GetTypeCode()
		{
			return TypeCode.String;
		}

		public Task<bool> IsConstant()
		{
			return Task.FromResult(false);
		}

		public Task<IExpr> ConstantOptimization()
		{
			return Task.FromResult(this as IExpr);
		}

		public async Task<object> Evaluate(Report rpt, Row row)
		{
			return await EvaluateString(rpt, row);
		}

		public Task<double> EvaluateDouble(Report rpt, Row row)
		{
			return Task.FromResult(rpt == null ? 1d : (double)rpt.TotalPages);
		}

		public Task<decimal> EvaluateDecimal(Report rpt, Row row)
		{
			return Task.FromResult(rpt == null ? 1m : (decimal)rpt.TotalPages);
		}

		public Task<int> EvaluateInt32(Report rpt, Row row)
		{
			return Task.FromResult(rpt == null ? 1 : rpt.TotalPages);
		}

		public Task<string> EvaluateString(Report rpt, Row row)
		{
			if (rpt == null)
				return Task.FromResult("1");
			return Task.FromResult(rpt.SubreportFirstPage > 0 ? Report.SubreportTotalPagesMark : rpt.TotalPages.ToString());
		}

		public Task<DateTime> EvaluateDateTime(Report rpt, Row row)
		{
			return Task.FromResult(DateTime.MinValue);
		}

		public Task<bool> EvaluateBoolean(Report rpt, Row row)
		{
			return Task.FromResult(true);
		}
	}
}
