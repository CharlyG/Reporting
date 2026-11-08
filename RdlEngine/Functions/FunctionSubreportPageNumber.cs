
using System;
using System.IO;
using System.Threading.Tasks;
using Majorsilence.Reporting.Rdl;


namespace Majorsilence.Reporting.Rdl
{
	/// <summary>
	/// CrystalEngine: the page number counted from the page the running subreport started on, as Crystal
	/// numbers a subreport's pages; the report's page number outside a subreport.
	/// </summary>
	[Serializable]
	internal class FunctionSubreportPageNumber : IExpr
	{
		/// <summary>
		/// Current page number
		/// </summary>
		public FunctionSubreportPageNumber() 
		{
		}

		public TypeCode GetTypeCode()
		{
			return TypeCode.Int32;
		}

		public Task<bool> IsConstant()
		{
			return Task.FromResult(false);
		}

		public Task<IExpr> ConstantOptimization()
		{	// not a constant expression
			return Task.FromResult(this as IExpr);
		}

		// Evaluate is for interpretation  
		public Task<object> Evaluate(Report rpt, Row row)
		{
            return rpt == null ? Task.FromResult((int) 0 as object) : Task.FromResult((int) rpt.SubreportPageNumber as object);
		}
		
		public Task<double> EvaluateDouble(Report rpt, Row row)
		{	
			return rpt == null? Task.FromResult(0d): Task.FromResult((double)rpt.SubreportPageNumber);
		}

        public Task<int> EvaluateInt32(Report rpt, Row row)
        {
            return rpt == null ? Task.FromResult(0) : Task.FromResult(rpt.SubreportPageNumber);
        }
		
		public async Task<decimal> EvaluateDecimal(Report rpt, Row row)
		{
			double result = await EvaluateDouble(rpt, row);

			return Convert.ToDecimal(result);
		}

		public async Task<string> EvaluateString(Report rpt, Row row)
		{
			double result = await EvaluateDouble(rpt, row);
			return result.ToString();
		}

		public async Task<DateTime> EvaluateDateTime(Report rpt, Row row)
		{
			double result = await EvaluateDouble(rpt, row);
			return Convert.ToDateTime(result);
		}

		public Task<bool> EvaluateBoolean(Report rpt, Row row)
		{
			return Task.FromResult(false);
		}
	}
}
