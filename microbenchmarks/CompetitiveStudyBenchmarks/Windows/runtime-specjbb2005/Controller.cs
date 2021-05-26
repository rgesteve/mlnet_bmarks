/*
 * Copyright (c) 2005 Standard Performance Evaluation Corporation (SPEC) All
 * rights reserved.
 * 
 * 2005/03/01 Veeru: Created first version - Multi-jvm Controller
 */
//Since this is multi-jvm controller we might not need it anyway....
using System;
using System.IO;
namespace Specjbb2005.src.spec.jbb
{
	/// <summary>
	/// Summary description for Controller.
	/// </summary>
	public class Controller
	{
		//  internal static readonly String   COPYRIGHT        = "SPECjbb2005,"
		//  + "Copyright (c) 2005 Standard Performance Evaluation Corporation (SPEC),"
		//  + "All rights reserved,"
		//  + "Licensed Materials - Property of SPEC";

		private static int    port             = 1500;

		private static String host             = "localhost";

		private static String defaultOutputDir = JBBmain.defaultOutputDir;

		private static String outputDir        = null;

		private static String propFile         = JBBmain.defaultPropsFileName;

		private static String runOutputSubDir  = null;

		private static int[]  sequenceOfWarehouses;

		private static int    numInst          = 1;

		private static void readProperties() 
		{
			JBBProperties prop = new JBBProperties(propFile);
			if (!prop.getProps()) 
			{
				Console.WriteLine("ERROR:  Properties File error; please start again");
				return;
			}
			// output directory
			outputDir = prop.getOptionalProperty("input.output_directory");
			if (outputDir == null) 
			{
				outputDir = defaultOutputDir;
			}
			// sequence of warehouses
			int i = 0;
			int seqLen;
			if (prop.sequenceOfWarehouses == null) 
			{
				seqLen = (prop.endingNumberWarehouses - prop.startingNumberWarehouses)
					/ prop.incrementNumberWarehouses + 1;
				sequenceOfWarehouses = new int[seqLen];
				for (int num_wh = prop.startingNumberWarehouses; num_wh <= prop.endingNumberWarehouses; num_wh += prop.incrementNumberWarehouses) 
				{
					sequenceOfWarehouses[i] = num_wh;
					i++;
				}
			}
			else 
			{
				seqLen = prop.sequenceOfWarehouses.Length;
				sequenceOfWarehouses = new int[seqLen];
				sequenceOfWarehouses = prop.sequenceOfWarehouses;
			}
			// number of JVM instances
			numInst = prop.jvm_instances;
		}

		private static void setOutputDirectory() 
		{
			RunSequencer subdirRs = new RunSequencer(outputDir, "SPECjbbMultiJVM.",
				null);
			runOutputSubDir = outputDir + Path.DirectorySeparatorChar + "SPECjbbMultiJVM."
				+ subdirRs.getSeqString();
			if (!initOutputDir(runOutputSubDir)) 
			{
				return;
			}
		}

		private static bool initOutputDir(String dir) 
		{
			DirectoryInfo output_directory_file = new DirectoryInfo(dir) ;
			if (output_directory_file.Exists) 
			{
				/*
				// File exists -- is it a directory?
				if (!output_directory_file.isDirectory()) 
				{
					Console.WriteLine("ERROR:  Specified input.output_directory is not a directory:  "
							   + dir);
					return false;
				}*/
			}
			else 
			{ // Specified directory does not exist -- try to create
				/*if (!output_directory_file.Create()) 
				{
					Console.WriteLine("ERROR:  Cannot create input.output_directory:  "
							   + dir);
					return false;
				}*/
				output_directory_file.Create();
			}
			return true;
		}//initOutputDir

		/*
		public static void Main(String[] args) 
		{
		}*/ //commented out since for CLR we don't need it.

		public Controller()
		{
			
		}
	}
}
