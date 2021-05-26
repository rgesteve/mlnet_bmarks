/*
 *
 *
 *
 * Copyright (c) 2000-2005 Standard Performance Evaluation Corporation (SPEC)
 *
 * All rights reserved.
 *
 * Copyright (c) 1996-2005 IBM Corporation, Inc. All rights reserved.
 *
 *
 * 2002/01/18 CJB: Modified putDollars to correct invalid conversion.
 *
 */
using System;
using System.IO;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Specjbb2005.src.spec.jbb.infra.Util
{
	/// <summary>
	/// Summary description for TransactionLogBuffer.
	/// </summary>
	/// 

	public class ScreenException : Exception 
	{
		/**
		* serialversionUID = 1 in first release
		*/
		private static readonly long serialVersionUID = 1L;

		//  // This goes right after each class/interface statement
		//  static readonly String       COPYRIGHT        = "SPECjbb2005,"
		//  + "Copyright (c) 2005 Standard Performance Evaluation Corporation (SPEC),"
		//  + "All rights reserved,"
		//  + "(C) Copyright IBM Corp., 1996 - 2005"
		//  + "All rights reserved,"
		//  + "Licensed Materials - Property of SPEC";

		public ScreenException() : base()
		{
			//super();
		}

		public ScreenException(String s):base(s) 
		{
			//super(s);
		}
	};

	public class TransactionLogBuffer
	{

		// This goes right after each class/interface statement
		static readonly String       COPYRIGHT   = "SPECjbb2005,"
		+ "Copyright (c) 2005 Standard Performance Evaluation Corporation (SPEC),"
		+ "All rights reserved,"
		+ "(C) Copyright IBM Corp., 1996 - 2005"
		+ "All rights reserved,"
		+ "US Government Users Restricted Rights,"
		+ "Use, duplication or disclosure restricted"
		+ "by GSA ADP Schedule Contract with IBM Corp.,"
		+ "Licensed Materials - Property of SPEC";

		public static readonly short alignLeft   = 0;

		public static readonly short alignCenter = 1;

		public static readonly short alignRight  = 2;

		//static FileOutputStream   fout        = new FileOutputStream(FileDescriptor.out);
		static   TextWriter			fout      = System.Console.Out; 

		internal static readonly float[]        fround    = 
		{
		    0.49f, 0.049f, 0.0049f, 0.00049f, 0.000049f, 0.0000049f,
		    0.00000049f, 0.000000049f, 0.0000000049f, 0.00000000049f
		};

		internal static readonly double[]       dround    = 
		{
			0.49d, 0.049d, 0.0049d, 0.00049d, 0.000049d, 0.0000049d,
			0.00000049d, 0.000000049d, 0.0000000049d, 0.00000000049d,
			0.000000000049d, 0.0000000000049d, 0.00000000000049d,
			0.000000000000049d, 0.0000000000000049d
		};

		internal static readonly float[]        f10_nth   = 
		{
			1.0f, 10.0f, 100.0f, 1000.0f, 10000.0f, 100000.0f, 1000000.0f,
			10000000.0f, 100000000.0f, 1000000000.0f
		};

		internal static readonly double[]       d10_nth   = 
		{
			1.0d, 10.0d, 100.0d, 1000.0d, 10000.0d, 100000.0d, 1000000.0d,
			10000000.0d, 100000000.0d, 1000000000.0d, 10000000000.0d,
			100000000000.0d, 1000000000000.0d, 10000000000000.0d,
			100000000000000.0d, 1000000000000000.0d, 10000000000000000.0d,
			100000000000000000.0d, 1000000000000000000.0d,
			10000000000000000000.0d
		};

		static readonly String       fractionPad = ".00000000000000000000";

		char[][]                  screenBuf;

		sbyte[]                    buf;

		int[]                     lineEnd;

		// CORECLR GregorianCalendar         calendar;

		int                       rows, cols;

		public TransactionLogBuffer() 
		{
			int x, y;
			rows = 24;
			cols = 80;
            // CORECLR calendar = new GregorianCalendar();
            buf = new sbyte[(cols + 1) * rows];
			screenBuf = new char[rows][];
			lineEnd = new int[rows];
			for (y = 0; y < rows; y++) 
			{
				screenBuf[y] = new char[cols];
				for (x = 0; x < cols; x++) 
				{
					screenBuf[y][x] = ' ';
				}
				lineEnd[y] = -1;
			}
		}

		public TransactionLogBuffer(int row, int col) 
		{
			int x, y;
			rows = row;
			cols = col;
            // CORECLR calendar = new GregorianCalendar();
            buf = new sbyte[(cols + 1) * rows];
			screenBuf = new char[rows][];
			lineEnd = new int[rows];
			for (y = 0; y < rows; y++) 
			{
				screenBuf[y] = new char[cols];
				for (x = 0; x < cols; x++) 
				{
					screenBuf[y][x] = ' ';
				}
				lineEnd[y] = -1;
			}
		}

		public TransactionLogBuffer(TransactionLogBuffer master) 
		{
			int y;
			rows = master.rows;
			cols = master.cols;
            // CORECLR calendar = new GregorianCalendar();
            buf = new sbyte[(cols + 1) * rows];
			screenBuf = new char[rows][];
			lineEnd = new int[rows];
			for (y = 0; y < rows; y++) 
			{
				screenBuf[y] = new char[cols];
				Array.Copy(master.screenBuf[y], 0, screenBuf[y], 0, cols);
				lineEnd[y] = master.lineEnd[y];
			}
		}

		public void clearBuffer() 
		{
			int x, y;
			for (y = 0; y < rows; y++) 
			{
				for (x = 0; x < cols; x++) 
				{
					screenBuf[y][x] = ' ';
				}
				lineEnd[y] = -1;
			}
		}

		private  void privSubstring(String s, int x, int y, int off, int len) 
		{
			int i;
			int slen;
			if (len > (s.Length - off)) 
			{
				slen = s.Length - off;
			}
			else 
			{
				slen = len;
			}
			//s.getChars(off, (off + slen), screenBuf[y], x);
			s.CopyTo(off, screenBuf[y], x,slen) ;
			if (slen < len) 
			{
				for (i = (x + slen); i < (x + len); i++) 
				{
					screenBuf[y][i] = ' ';
				}
			}
		}

		public void putSubstring(String s, int x, int y, int off, int len) 
		{
			if ((y < 0) | (y >= rows)) 
			{
				ScreenException e = new ScreenException("Screen: row "
					+ y.ToString() + " out of range");
				throw e;
			}
			else 
			{
				if ((x < 0) | (x >= cols)) 
				{
					ScreenException e = new ScreenException("Screen: column "
						+ x.ToString() + " out of range");
					throw e;
				}
				else 
				{
					if ((x + len) > cols) 
					{
						ScreenException e = new ScreenException("Screen: length "
							+ len.ToString() + " exceeds left margin");
						throw e;
					}
					else 
					{
						privSubstring(s, x, y, off, len);
						if ((x + len) > lineEnd[y])
							lineEnd[y] = (x + len);
					}
				}
			}
		}

		private void privText(String s, int x, int y, int len) 
		{
			int i;
			int slen;
			if (len > s.Length) 
			{
				slen = s.Length;
			}
			else 
			{
				slen = len;
			}
			//s.getChars(0, slen, screenBuf[y], x);
			s.CopyTo(0,screenBuf[y],x,slen);
			if (slen < len) 
			{
				for (i = (x + slen); i < (x + len); i++) 
				{
					screenBuf[y][i] = ' ';
				}
			}
		}

		public void putText(String s, int x, int y, int len) 
		{
			if ((y < 0) | (y >= rows)) 
			{
				ScreenException e = new ScreenException("Screen: row "
					+ y.ToString() + " out of range");
				throw e;
			}
			else 
			{
				if ((x < 0) | (x >= cols)) 
				{
					ScreenException e = new ScreenException("Screen: column "
						+ x.ToString() + " out of range");
					throw e;
				}
				else 
				{
					if ((x + len) > cols) 
					{
						ScreenException e = new ScreenException("Screen: length "
							+ len.ToString() + " exceeds left margin");
						throw e;
					}
					else 
					{
						privText(s, x, y, len);
						if ((x + len) > lineEnd[y])
							lineEnd[y] = (x + len);
					}
				}
			}
		}

		private void privText(String s, int x, int y, int len, short align) 
		{
			int i, first;
			int slen;
			if (len > s.Length) 
			{
				slen = s.Length;
				if (align == alignRight) 
				{
					first = x + (len - slen);
				}
				else 
				{
					if (align == alignCenter) 
					{
						first = x + ((len - slen) / 2);
					}
					else 
					{
						first = x;
					}
				}
			}
			else 
			{
				slen = len;
				first = x;
			}
			if (first > x) 
			{
				for (i = x; i < first; i++) 
				{
					screenBuf[y][i] = ' ';
				}
			}
			//s.getChars(0, slen, screenBuf[y], first);
			s.CopyTo(0,screenBuf[y],first,slen);
			if (slen < len) 
			{
				for (i = (first + slen); i < (x + len); i++) 
				{
					screenBuf[y][i] = ' ';
				}
			}
		}

		public void putText(String s, int x, int y, int len, short align) 
		{
			if ((y < 0) | (y >= rows)) 
			{
				ScreenException e = new ScreenException("Screen: row "
					+ y.ToString() + " out of range");
				throw e;
			}
			else 
			{
				if ((x < 0) | (x >= cols)) 
				{
					ScreenException e = new ScreenException("Screen: column "
						+ x.ToString() + " out of range");
					throw e;
				}
				else 
				{
					if ((x + len) > cols) 
					{
						ScreenException e = new ScreenException("Screen: length "
							+ len.ToString() + " exceeds left margin");
						throw e;
					}
					else 
					{
						privText(s, x, y, len, align);
						if ((x + len) > lineEnd[y])
							lineEnd[y] = (x + len);
					}
				}
			}
		}

		private void privCharFill(char c, int x, int y, int len) 
		{
			int i;
			for (i = x; i < (x + len); i++) 
			{
				screenBuf[y][i] = c;
			}
		}

		public void putCharFill(char c, int x, int y, int len) 
		{
			if ((y < 0) | (y >= rows)) 
			{
				ScreenException e = new ScreenException("Screen: row "
					+ y.ToString() + " out of range");
				throw e;
			}
			else 
			{
				if ((x < 0) | (x >= cols)) 
				{
					ScreenException e = new ScreenException("Screen: column "
						+ x.ToString() + " out of range");
					throw e;
				}
				else 
				{
					if ((x + len) > cols) 
					{
						ScreenException e = new ScreenException("Screen: length "
							+ len.ToString() + " exceeds left margin");
						throw e;
					}
					else 
					{
						privCharFill(c, x, y, len);
						if ((x + len) > lineEnd[y])
							lineEnd[y] = (x + len);
					}
				}
			}
		}

		private void privChar(char c, int x, int y) 
		{
			screenBuf[y][x] = c;
		}

		public void putChar(char c, int x, int y) 
		{
			if ((y < 0) | (y >= rows)) 
			{
				ScreenException e = new ScreenException("Screen: row "
					+ y.ToString() + " out of range");
				throw e;
			}
			else 
			{
				if ((x < 0) | (x >= cols)) 
				{
					ScreenException e = new ScreenException("Screen: column "
						+ x.ToString() + " out of range");
					throw e;
				}
				else 
				{
					screenBuf[y][x] = c;
					if ((x + 1) > lineEnd[y])
						lineEnd[y] = (x + 1);
				}
			}
		}

		private int privIntLeadingZeros(int iz, int x, int y, int len) 
		{
			int i, dividend, remainder;
			char[] screenLine;
			screenLine = screenBuf[y];
			for (i = (x + len - 1); i >= x; i--) 
			{
				dividend = iz / 10;
				remainder = iz - dividend * 10;
				iz = dividend;
				screenLine[i] = (char) ('0' + remainder);
			}
			return iz;
		}

		private int privInt(int iz, int x, int y, int len) 
		{
			int i, dividend, remainder;
			char[] screenLine;
				char sign = ' ';
			if (iz < 0) 
			{
				iz = -iz;
				sign = '-';
			}
			screenLine = screenBuf[y];
			i = (x + len - 1);
			do 
			{
				dividend = iz / 10;
				remainder = iz - dividend * 10;
				iz = dividend;
				screenLine[i] = (char) ('0' + remainder);
				i--;
			}
			while ((i >= x) && (iz != 0));
			if (i >= x) 
			{
				screenLine[i] = sign;
				i--;
			}
			else 
			{
				if (sign == '-')
					iz = -1;
			}
			for (; i >= x; i--) 
			{
				screenLine[i] = ' ';
			}
			return iz;
		}

		private long privLong(long iz, int x, int y, int len) 
		{
			int i;
			long dividend, remainder;
			char[] screenLine;
				char sign = ' ';
			if (iz < 0) 
			{
				iz = -iz;
				sign = '-';
			}

			screenLine = screenBuf[y];
			i = (x + len - 1);
			do 
			{
				dividend = iz / 10;
				remainder = iz - dividend * 10;
				iz = dividend;
				screenLine[i] = (char) ('0' + remainder);
				i--;
			}
			while ((i >= x) && (iz != 0));
			if (i >= x) 
			{
				screenLine[i] = sign;
				i--;
			}
			else 
			{
				if (sign == '-')
					iz = -1;
			}
			for (; i >= x; i--) 
			{
				screenLine[i] = ' ';
			}
			return iz;
		}

		private int privInt(int iz, char sign, int x, int y, int len) 
		{
			int i, dividend, remainder;
			char[] screenLine;
			screenLine = screenBuf[y];
			i = (x + len - 1);
			do 
			{
				dividend = iz / 10;
				remainder = iz - dividend * 10;
				iz = dividend;
				screenLine[i] = (char) ('0' + remainder);
				i--;
			}
			while ((i >= x) && (iz != 0));
			if (i >= x) 
			{
				screenLine[i] = sign;
				i--;
			}
			else 
			{
				if (sign != ' ')
					iz = -1;
			}
			for (; i >= x; i--) 
			{
				screenLine[i] = ' ';
			}
			return iz;
		}

		public void putInt(int i, int x, int y, int len) 
		{
			if ((y < 0) | (y >= rows)) 
			{
				ScreenException e = new ScreenException("Screen: row "
					+ y.ToString() + " out of range");
				throw e;
			}
			else 
			{
				if ((x < 0) | (x >= cols)) 
				{
					ScreenException e = new ScreenException("Screen: column "
						+ x.ToString() + " out of range");
					throw e;
				}
				else 
				{
					if ((x + len) > cols) 
					{
						ScreenException e = new ScreenException("Screen: length "
							+ len.ToString() + " exceeds left margin");
						throw e;
					}
					else 
					{
						if (len <= 0) 
						{
							ScreenException e = new ScreenException(
								"Screen: length  must be greater than 0 was"
								+ len.ToString());
							throw e;
						}
						else 
						{
							i = privInt(i, x, y, len);
							if (i != 0) 
							{ // numeric overflow
								this.privCharFill('*', x, y, len);
							}
							if ((x + len) > lineEnd[y])
								lineEnd[y] = (x + len);
						}
					}
				}
			}
		}

		public void putLong(long l, int x, int y, int len) 
		{
			if ((y < 0) | (y >= rows)) 
			{
				ScreenException e = new ScreenException("Screen: row "
					+ y.ToString() + " out of range");
				throw e;
			}
			else 
			{
				if ((x < 0) | (x >= cols)) 
				{
					ScreenException e = new ScreenException("Screen: column "
						+ x.ToString() + " out of range");
					throw e;
				}
				else 
				{
					if ((x + len) > cols) 
					{
						ScreenException e = new ScreenException("Screen: length "
							+ len.ToString() + " exceeds left margin");
						throw e;
					}
					else 
					{
						if (len <= 0) 
						{
							ScreenException e = new ScreenException(
								"Screen: length  must be greater than 0 was"
								+ len.ToString());
							throw e;
						}
						else 
						{
							l = privLong(l, x, y, len);
							if (l != 0) 
							{ // numeric overflow
								this.privCharFill('*', x, y, len);
							}

							if ((x + len) > lineEnd[y])
								lineEnd[y] = (x + len);
						}
					}
				}
			}
		}

		public void putDouble(String d, int x, int y, int len) 
		{
			putText(d, x, y, len, alignRight);
		}

		public void putDouble(double d, int x, int y, int len, int frac) 
		{
			int i;
			int temp, int_part, fract_part;
			double absolute;
			bool negative = false;
			if ((y < 0) | (y >= rows)) 
			{
				ScreenException e = new ScreenException("Screen: row "
					+ y.ToString() + " out of range");
				throw e;
			}
			else 
			{
				if ((x < 0) | (x >= cols)) 
				{
					ScreenException e = new ScreenException("Screen: column "
						+ x.ToString() + " out of range");
					throw e;
				}
				else 
				{
					if ((x + len) > cols) 
					{
						ScreenException e = new ScreenException("Screen: length "
							+ len.ToString() + " exceeds left margin");
						throw e;
					}
					else 
					{
						if (frac < 0) 
						{
							frac = 0;
						}
						else 
						{
							if (frac > 19) 
							{
								frac = 19;
							}
						}
						if (frac >= (len - 1)) 
						{
							ScreenException e = new ScreenException(
								"Screen: factional digits "
								+ frac.ToString()
								+ " too large for field length "
								+ len.ToString());
							throw e;
						}
					}
				}
			}
			/* make "d" positive */
			if (d < 0.0d) 
			{
				absolute = -d;
			}
			else 
			{
				absolute = d;
			}
			/* Separate number into integer and fractional parts */
			int_part = (int) absolute;
			fract_part = (int) ((absolute - int_part) * d10_nth[frac] + 0.5d);
			/* let the integer portion contain the sign */
			if (d < 0.0d) 
			{
				int_part = -int_part;
				negative = true;
			}
			i = len - frac - 1;// offset to decimal in field
			/* format the fraction, with leading zeros */
			temp = privIntLeadingZeros(fract_part, x + i + 1, y, frac);
			/* put in the decimal */
			screenBuf[y][x + i] = '.';
			/* format the integer part */
			if (i > 0) 
			{
				temp = privInt(int_part, x, y, i);
				if (temp != 0L) 
				{
					privCharFill('*', x, y, len);
				}
			}
			else 
			{
				if (negative) 
				{
					privCharFill('*', x, y, len);
				}
			}
			if ((x + len) > lineEnd[y])
				lineEnd[y] = (x + len);
		}

		//Do I need to pass the DatimeTime as ref param?
		public void putDate(ref DateTime d, int x, int y, int len) 
		{
			if ((y < 0) | (y >= rows)) 
			{
				ScreenException e = new ScreenException("Screen: row "
					+ y.ToString() + " out of range");
				throw e;
			}
			else 
			{
				if ((x < 0) | (x >= cols)) 
				{
					ScreenException e = new ScreenException("Screen: column "
						+ x.ToString() + " out of range");
					throw e;
				}
				else 
				{
					if ((x + len) > cols) 
					{
						ScreenException e = new ScreenException("Screen: length "
							+ len.ToString() + " exceeds left margin");
						throw e;
					}
					else 
					{
						if (len < 10) 
						{
							ScreenException e = new ScreenException(
								"Screen: length " + len.ToString()
								+ " too small for date");
							throw e;
						}
					}
				}
			}
			// this.putText("00-00-1900", x, y, 10);
			//calendar.setTime(d);Directly use the DATE time from DATETime
			this.privIntLeadingZeros(d.Day/*calendar.get(Calendar.DATE)*/, x, y, 2);
			this.privChar('-', x + 2, y);
			this.privIntLeadingZeros(d.Month/*calendar.get(Calendar.MONTH)*/ + 1, x + 3, y, 2);
			this.privChar('-', x + 5, y);
			this.privIntLeadingZeros(d.Year/*calendar.get(Calendar.YEAR)*/, x + 6, y, 4);
			if ((x + len) > lineEnd[y])
				lineEnd[y] = (x + len);
		}

		//Do I need to pass the DatimeTime as ref param?
		public void putTime(ref DateTime d, int x, int y, int len) 
		{
			if ((y < 0) | (y >= rows)) 
			{
				ScreenException e = new ScreenException("Screen: row "
					+ y.ToString() + " out of range");
				throw e;
			}
			else 
			{
				if ((x < 0) | (x >= cols)) 
				{
					ScreenException e = new ScreenException("Screen: column "
						+ x.ToString() + " out of range");
					throw e;
				}
				else 
				{
					if ((x + len) > cols) 
					{
						ScreenException e = new ScreenException("Screen: length "
							+ len.ToString() + " exceeds left margin");
						throw e;
					}
					else 
					{
						if (len < 8) 
						{
							ScreenException e = new ScreenException(
								"Screen: length " + len.ToString()
								+ " too small for time");
							throw e;
						}
					}
				}
			}
			// this.putText("00:00:00", x, y, 8);
			//calendar.setTime(d);
			this.privIntLeadingZeros(d.Hour/*calendar.get(Calendar.HOUR)*/, x, y, 2);
			this.privChar(':', x + 2, y);
			this.privIntLeadingZeros(d.Minute/*calendar.get(Calendar.MINUTE)*/, x + 3, y, 2);
			this.privChar(':', x + 5, y);
			this.privIntLeadingZeros(d.Second/*calendar.get(Calendar.SECOND)*/, x + 6, y, 2);
			if ((x + len) > lineEnd[y])
				lineEnd[y] = (x + len);
		}

		public void putDollars(Decimal d, int x, int y, int len) 
		{
			String dollarValue = d.ToString();
			int size = dollarValue.Length;
			// first check to see if d is negative or positive
			if (System.Math.Sign(d) > 0) 
			{
				screenBuf[y][x] = '$';
				putText(dollarValue, x + 1, y, size, alignRight);
			}
			else 
			{
				putText(dollarValue, x, y, size, alignRight);
			}
			if ((x + len) > lineEnd[y])
				lineEnd[y] = (x + len);
		}

		public void putDollars(double d, int x, int y, int len) 
		{
			int frac = 2;
			int i, int_part, fract_part;
			int temp;
			double absolute;
			bool negative = false;
			if ((y < 0) | (y >= rows)) 
			{
				ScreenException e = new ScreenException("Screen: row "
					+ y.ToString() + " out of range");
				throw e;
			}
			else 
			{
				if ((x < 0) | (x >= cols)) 
				{
					ScreenException e = new ScreenException("Screen: column "
						+ x.ToString() + " out of range");
					throw e;
				}
				else 
				{
					if ((x + len) > cols) 
					{
						ScreenException e = new ScreenException("Screen: length "
							+ len.ToString() + " exceeds left margin");
						throw e;
					}
					else 
					{
						if (frac >= (len - 1)) 
						{
							ScreenException e = new ScreenException(
								"Screen: factional digits "
								+ frac.ToString()
								+ " too large for field length "
								+ len.ToString());
							throw e;
						}
					}
				}
			}
			/* make "d" positive */
			if (d < 0.0d) 
			{
				absolute = -d;
			}
			else 
			{
				absolute = d;
			}
			/* Separate number into integer and fractional parts */
			int_part = (int) absolute;
			fract_part = (int) ((absolute - int_part) * d10_nth[frac] + 0.5d);
			/*
			 * 2002/01/18 CJB fract_part can equal 100 in some cases, so need to
			 * check it and alter int_part so that validation check will pass.
			 */
			if (fract_part > 99) 
			{
				int_part++;
				fract_part = fract_part - 100;
			}
			/* let the integer portion contain the sign */
			if (d < 0.0d) 
			{
				int_part = -int_part;
				negative = true;
			}
			i = len - frac - 1;// offset to decimal in field
			/* format the fraction, with leading zeros */
			temp = privIntLeadingZeros(fract_part, x + i + 1, y, frac);
			/* put in the decimal */
			screenBuf[y][x + i] = '.';
			/* format the integer part */
			if (i > 0) 
			{
				char sign = '$';
				if (negative)
					sign = '-';
				temp = privInt(int_part, sign, x, y, i);
				if (temp != 0L) 
				{
					privCharFill('*', x, y, len);
				}
			}
			else 
			{
				if (negative) 
				{
					privCharFill('*', x, y, len);
				}
			}
			if ((x + len) > lineEnd[y])
				lineEnd[y] = (x + len);
		}

		public String getLine(int line) 
		{
			return new String(screenBuf[line]);//new String(screenBuf[line]);
		}

		public int getLineCount() 
		{
			return rows;
		}

		public void display(/*Stream*/TextWriter out1) 
		{
			int x, y;
			int len;
			char[] line;
			try {
				for (y = 0; y < rows; y++) {
					len = lineEnd[y];
					if (len > 0) {
						line = screenBuf[y];
						String sline = new String(line, 0, len);
						out1.WriteLine(sline);
					}
					else {
						out1.WriteLine();
					}
				}
				out1.Flush();
			}
			catch (Exception e) {
				Console.WriteLine("IOException:" + e.Message);
				Console.WriteLine(e.StackTrace);
				ScreenException except = new ScreenException("file write error");
				throw except;
			}
		}

		public String[] validate() 
		{
			int x, y;
			int len;
			char[] line;
				String[] sline = new String[rows];
					for (y = 0; y < rows; y++) 
					{
						len = lineEnd[y];
						if (len > 0) 
						{
							line = screenBuf[y];
							sline[y] = new String(line, 0, len);
						}
						else 
						{
                    sline[y] = String.Empty; // CORECLR String.Copy("");//new String("");
						}
					}
			return sline;
		}

		// CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
		public void display() 
		{
			int x, y;
			int len;
			int bIndex;
			char[] line;
				try 
				{
					bIndex = 0;
					for (y = 0; y < rows; y++) 
					{
						len = lineEnd[y];
						if (len > 0) 
						{
							line = screenBuf[y];
							for (x = 0; x < len; x++) 
							{
								buf[bIndex++] = (sbyte) line[x];
							}
						}
						buf[bIndex++] = 10;
					}
					//fout.Write(buf, 0, bIndex);
					Console.Out.Write(buf.ToString(),0,bIndex) ;//same as jbb1.0
				}
				catch (IOException e) 
				{
					Console.WriteLine("IOException:" + e.Message);
					Console.WriteLine(e.StackTrace);
					ScreenException except = new ScreenException("screen write error");
					throw except;
				}
		}

	}//class TransactionLogBuffer

}
