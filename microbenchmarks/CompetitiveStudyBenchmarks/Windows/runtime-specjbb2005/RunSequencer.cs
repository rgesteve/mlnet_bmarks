/*
 *
 * Copyright (c) 2000-2005 Standard Performance Evaluation Corporation (SPEC)
 *               All rights reserved.
 * Copyright (c) 2000-2005 Hewlett-Packard        All rights reserved.
 *
 * This source code is provided as is, without any express or implied warranty.
 *
 */
using System;
using System.IO;

namespace Specjbb2005.src.spec.jbb
{
	/// <summary>
	/// Summary description for ResFilter.
	/// </summary>
	/// 
	/* I don;t think we need it at all. same as Specjbb1.01
	public class ResFilter
	{
		// This goes right after each class/interface statement
		static const String COPYRIGHT    = "SPECjbb2005,"
		+ "Copyright (c) 2000-2005 Standard Performance Evaluation Corporation (SPEC),"
		+ "All rights reserved,"
		+ "Copyright (c) 2000-2005 Hewlett-Packard,"
		+ "All rights reserved,"
		+ "Licensed Materials - Property of SPEC";

		String              resultPrefix;                                                  // =

		// "SPECjbb.";
		String              resultSuffix = ".results";

		int                 resultPrefixLen;                                               // =

		// resultPrefix.length();
		int                 resultSuffixStart;

		public ResFilter(String prefix, String suffix) 
		{
			resultPrefix = prefix;
			resultPrefixLen = resultPrefix.Length;
			String resultSuffix = suffix;
			resultSuffixStart = resultPrefixLen + 3;
		}

		public bool accept(File dir, String name) 
		{
			return ((name.StartsWith(resultPrefix)) && ((name
				.Substring(resultSuffixStart).Equals(resultSuffix)) || (resultSuffixStart == name
				.Length)));
		}

	}//ResFilter


	public class PrintLastSeq 
	{
		// This goes right after each class/interface statement
		static const String COPYRIGHT = "SPECjbb2005,"
		+ "Copyright (c) 2005 Standard Performance Evaluation Corporation (SPEC),"
		+ "All rights reserved,"
		+ "Copyright (c) 2005 Hewlett-Packard,"
		+ "All rights reserved,"
		+ "Licensed Materials - Property of SPEC";

		public static void main(String[] args) 
		{
			String dir = ".";
			if (args.Length == 1)
				dir = args[0];
			String resultPrefix = "SPECjbb.";
			String resultSuffix = ".raw";
			// int resultPrefixLen = resultPrefix.length();
			RunSequencer rs = new RunSequencer(dir, resultPrefix, resultSuffix);
			Console.WriteLine(rs.padNumber(rs.getSeq() - 1));
		}
	}//class PrintLastSeq
	*/
	public class RunSequencer 
	{
		// This goes right after each class/interface statement
		//  static readonly String COPYRIGHT = "SPECjbb2005,"
		//  	+ "Copyright (c) 2005 Standard Performance Evaluation Corporation (SPEC),"
		//  	+ "All rights reserved,"
		//  	+ "Copyright (c) 2005 Hewlett-Packard,"
		//  	+ "All rights reserved,"
		//  	+ "Licensed Materials - Property of SPEC";

		String              resultPrefix;

		String              resultSuffix;

		int                 resultPrefixLen;

		//File                f1;
		DirectoryInfo		f1 ;

		public RunSequencer(String dir, String prefix, String suffix) 
		{
			f1 = new DirectoryInfo(dir) ;//new File(dir);
			resultPrefix = prefix;
			resultPrefixLen = resultPrefix.Length;
			resultSuffix = suffix;
		}

		public int getSeq () 
		{
			int i;
			FileInfo[]	ls ;

			String filter = resultPrefix + "*" + resultSuffix ;
			ls = f1.GetFiles/*GetFilesInDirectory*/(/*dire,*/filter) ;
			int foo = 0;
			int max = 0;
			String fooString;
			int fooLen;
			int j;
			bool skip = false;

			for(i=0;ls != null && i<ls.Length;i ++)
			{
				//fooString = ls[i].Name.Substring(resultPrefixLen, (resultPrefixLen + 3 - resultPrefixLen));
                fooString = ls[i].Name.Substring(resultPrefixLen, 3);
				fooLen = fooString.Length;
				skip = false;
				for (j=0; j < fooLen; j++) 
				{
					//if ( ! Character.isDigit(fooString.charAt(j))) 
					if(!char.IsDigit(fooString[j]))
					{
						skip = true;
					}
				}
				if ( ! skip ) 
				{
					foo = int.Parse(fooString) ;//Integer.parseInt(fooString);
					if (foo > max)
						max = foo;
				}
			}
			return (max + 1);
		}//getSeq

		public String getSeqString() 
		{
			return padNumber(getSeq());
		}

		public String padNumber(int n) 
		{
			String returnString = "" + n;
			int returnStringLength = returnString.Length;
			if (returnStringLength == 1) 
			{
				returnString = "00" + returnString;
			}
			if (returnStringLength == 2) 
			{
				returnString = "0" + returnString;
			}
			return returnString;
		}
	}
}
