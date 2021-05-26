using System;
using System.Text;
using System.Diagnostics;

namespace Specjbb2005.src.spec.jbb
{
	/// <summary>
	/// Summary description for JBBLogFormatter.
	/// </summary>
	public class JBBLogFormatter //???
	{
		//  static readonly String           COPYRIGHT       = "SPECjbb2005,"
		//  + "Copyright (c) 2005 Standard Performance Evaluation Corporation (SPEC),"
		//  + "All rights reserved,"
		//  + "(C) Copyright IBM Corp., 1996 - 2005"
		//  + "All rights reserved,"
		//  + "Licensed Materials - Property of SPEC";

		protected static readonly String ARROW_SEPARATOR = " -> ";

		protected static readonly String NEW_LINE_STRING = Environment.NewLine ; //("line.separator");

		//TODO:Common we don;t need this formating crap...
		/*
		public String format(LogRecord record) 
		{
			StringBuilder sb = new StringBuilder(super.format(record));
			int pos = sb.IndexOf(JBBLogFormatter.NEW_LINE_STRING);
			if (pos != -1) 
			{
				int end = pos + JBBLogFormatter.NEW_LINE_STRING.Length;
				sb.Replace(pos, end, JBBLogFormatter.ARROW_SEPARATOR);
			}
			return (sb.ToString());
		}*/
	}
}
