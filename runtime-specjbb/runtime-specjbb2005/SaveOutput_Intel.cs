/*
 * From JDC Tech Tips
 *
 */

/*
 *
 * Copyright (c) 2000 Standard Performance Evaluation Corporation (SPEC)
 *               All rights reserved.
 *
 * This source code is provided as is, without any express or implied warranty.
 *
 */

using System;
using System.IO;
using System.Collections;

namespace Specjbb2005.src.spec.jbb
{
	/// <summary>
	/// Summary description for SaveOutput.
	/// </summary>
	public class SaveOutput : StreamWriter
	{
		// This goes right after each class/interface statement
		//  internal static readonly String COPYRIGHT =
		//  	"SPECjbb2000,"+
		//  	"Copyright (c) 2000 Standard Performance Evaluation Corporation (SPEC),"+
		//  	"All rights reserved,"+
		//  	"Licensed Materials - Property of SPEC";

		internal static BinaryWriter		logfile;
		internal static TextWriter		oldStdout;
		internal static TextWriter		oldStderr;
		
		internal SaveOutput(Stream ps) : base(ps)
		{
			//super(ps);
		}//SaveOutput constructor
        
		/*
		public SaveOutput()
		{
            
		}//SaveOutput constructor*/

		// Starts copying stdout and stderr to the file f.
		public static void start(String f) //throws IOException {
		{
			// Save old settings.
			oldStdout = Console.Out ;
			oldStderr = Console.Error ;

			// Create/Open logfile.
			//logfile = new PrintStream(new BufferedOutputStream(new FileOutputStream(f)));
			//logfile = new FileStream(f,FileMode.Create,FileAccess.Write) ;
			logfile = new BinaryWriter(new FileStream(f,FileMode.Create,FileAccess.ReadWrite)) ;
            // Start redirecting the output.
            //System.setOut(new SaveOutput(System.out));

            // CORECLR Console.SetOut(new SaveOutput(Console.OpenStandardOutput())) ;
            //System.setErr(new SaveOutput(System.err));
            // CORECLR Console.SetError(new SaveOutput(Console.OpenStandardError())) ;
        }//start

        // Restores the original settings.
        public static void stop() 
		{
			Console.SetOut(oldStdout);
			Console.SetError(oldStderr);
			try 
			{
				//fs.Close() ;
				// CORECLR logfile..Close() ;
			} 
			catch (IOException e) 
			{
				Console.Out.WriteLine(e.StackTrace) ;
			}
		}//stop

		public override void Write(int b) 
		{
			try 
			{
				logfile.Write(b) ;
			} 
			catch (Exception e) 
			{
				Console.Out.WriteLine(e.StackTrace) ;
				//setError();
			}
			base.Write(b);
		}
		
		// PrintStream override.
		public override void Write(char[] buf, int off, int len) 
			//public override void Write(byte[] buf, int off, int len) 
		{
			try
			{
				logfile.Write(buf,off,len);
			} 
			catch (IOException e) 
			{
				Console.Out.WriteLine(e.StackTrace) ;
				//setError();
			}
			base.Write(buf, off, len);
		}//Write
    
	}//SaveOutput

}
