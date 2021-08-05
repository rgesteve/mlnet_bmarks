/*
 * 
 * Copyright (c) 2000-2005 Standard Performance Evaluation Corporation (SPEC) All
 * rights reserved. Copyright (c) 1996-2005 IBM Corporation, Inc. All rights
 * reserved.
 */
using System;
using System.IO;
using System.Collections;
using System.Diagnostics;

namespace Specjbb2005.src.spec.jbb
{
	/// <summary>
	/// Summary description for JBBProperties.
	/// </summary>
	public class JBBProperties
	{

		// This goes right after each class/interface statement
		//  static readonly String          COPYRIGHT                                 = "SPECjbb2005,"
		//  + "Copyright (c) 2005 Standard Performance Evaluation Corporation (SPEC),"
		//  + "All rights reserved,"
		//  + "(C) Copyright IBM Corp., 1996 - 2005"
		//  + "All rights reserved,"
		//  + "Licensed Materials - Property of SPEC";

		//private Properties           PropertiesForBatch;
		private /*PropertyCollection*/Hashtable PropertiesForBatch = new Hashtable();	

		String                       val;

		public int                   warehousePopulationBase;

		public int                   orderlinesPerOrder;

		public int                   rampupSeconds;

		public int                   measurementSeconds;

		public int                   expectedPeakWarehouse                     = 4;                                              // defaults

		// to 4
		public bool                  deterministicRandomSeed                   = false;

		public int                   jvm_instances                             = 1;

		public float                 per_jvm_warehouse_rampup                  = 0f;

		public float                 per_jvm_warehouse_rampdown                = 0f;

		public int                   waitTimePercent;

		public bool                  showWarehouseDetail                       = false;

		public int                   startingNumberWarehouses;

		public int                   incrementNumberWarehouses;

		public int                   endingNumberWarehouses;

		public int[]                 sequenceOfWarehouses;

		public bool                  steadyStateFlag;

		public bool                  screenWriteFlag;

		public bool                  checkThroughput;

		public float                 minBTPSRatio;

		public static int            overrideItemTableSize                     = 20000;

		public static bool           uniformRandomItems                        = true;

		public static bool           printPropertiesAndArgs                    = true;

		private TraceLevel                applicationLoggingLevel                   = TraceLevel.Verbose;

		private static readonly float   COMPLIANT_RATE_per_jvm_warehouse_rampup   = 3.0f;

		private static readonly float   COMPLIANT_RATE_per_jvm_warehouse_rampdown = 20.0f;

		private static readonly int     COMPLIANT_RATE_warehousePopulationBase    = 60;

		private static readonly int     COMPLIANT_RATE_orderlinesPerOrder         = 10;

		private static readonly int     COMPLIANT_RATE_rampupSeconds              = 30;

		private static readonly int     COMPLIANT_RATE_measurementSeconds         = 240;

		private static readonly int     COMPLIANT_RATE_waitTimePercent            = 0;

		private static readonly bool    COMPLIANT_RATE_steadyStateFlag            = true;

		private static readonly bool    COMPLIANT_RATE_screenWriteFlag            = false;

		private static readonly bool    COMPLIANT_RATE_uniformRandomItems         = true;

		private static readonly int     COMPLIANT_RATE_overrideItemTableSize      = 20000;

		private static readonly bool    COMPLIANT_RATE_deterministicRandomSeed    = false;

		private static readonly TraceLevel   COMPLIANT_RATE_applicationLoggingLevel    = TraceLevel.Info;


		//Basically tries to load the file to PropertyCollection.
		public void Load(FileStream PropertiesFile)
		{
			StreamReader SR = new StreamReader((Stream)PropertiesFile) ;

			char[] seperator = new char[1];
			seperator[0]='=';
			while(true)
			{
				String ss = SR.ReadLine() ;
				if(ss == null)
				{
					break ;
				}
				if(ss.StartsWith("#") || ss=="")
				{
					continue ;
				}
				else
				{
					String[] key = ss.Split(seperator) ;
                              lock (PropertiesForBatch)
                              {
					    PropertiesForBatch.Add(key[0],key[1]) ;
                              }
					//Console.WriteLine("Key={0}", key[0]) ;
					//Console.WriteLine("Value={0}", key[1]) ;
				}
			}
		}
		public JBBProperties(String propertiesFileName) 
		{
			Console.WriteLine("");
			Console.WriteLine("Loading properties from " + propertiesFileName);
			try 
			{
				FileStream PropertiesFile = new FileStream(propertiesFileName,FileMode.Open,FileAccess.Read);
				PropertiesForBatch = new Hashtable() /*PropertyCollection()*/ ;
				this.Load(PropertiesFile) ; //PropertiesForBatch.load(PropertiesFile);
			}
			catch (IOException e) 
			{
				Trace.WriteLineIf(JBButil.getLog().TraceWarning,
					"ERROR:  Properties File error; please start again " + e.Message);
				return;
			}
			val = getOptionalProperty("input.include_file");
			if (val != null) 
			{
				try 
				{
					FileStream IncludedPropertiesFile = new FileStream(val,FileMode.Open,FileAccess.Read);
					this.Load(IncludedPropertiesFile);
					//PropertiesForBatch.load(IncludedPropertiesFile);
				}
				catch (IOException e) 
				{
					Trace.WriteLineIf(JBButil.getLog().TraceWarning,
                        "ERROR:  Properties File error with included properties file; please start again " + e.Message);
                    return;
				}
			}
		}

		public String getRequiredProperty(String s) 
		{
			try 
			{
				return ((String)PropertiesForBatch[s]).Trim();
				//return (PropertiesForBatch.getProperty(s).trim());
			}
			catch (NullReferenceException e) 
			{
				Trace.WriteLineIf(JBButil.getLog().TraceWarning,
					"ERROR:  Property " + s
					+ " not specified; please add to Properties File " + e.Message);
                return (null);
			}
		}

		public void setRequiredProperty(String property, String value1) 
		{
			try 
			{
				// return (PropertiesForBatch.getProperty(s).trim());
                        lock (PropertiesForBatch)
                        {
				    PropertiesForBatch.Add(property, value1);
                        }
			}
			catch (NullReferenceException e) 
			{
				Trace.WriteLineIf(JBButil.getLog().TraceWarning, "ERROR: setting " + property + e.Message);
            }
		}

		public String getOptionalProperty(String s) 
		{
            try
            {
                return ((String)PropertiesForBatch[s]).Trim();
            }
            catch (Exception e)
            {
                Console.WriteLine(String.Format("Optional proerty <{0}> is not providied.", s));
                return (null);
            }
        }

		public bool getProps() 
		{
			String prefix = "input.";
			String val;
			int tmpValue = 0;
			bool retval = true;
			bool valid_warehouse_sequence = false;
			sequenceOfWarehouses = null;
			val = getRequiredProperty(prefix + "suite");
			if (val != null) 
			{
				if (val.Equals("SPECjbb")) 
				{
					checkThroughput = false;
				}
				else 
				{
					Console.WriteLine("ERROR:  Property file error");
					Console.WriteLine("   suite must be SPECjbb");
					retval = false;
				}
			}
			else 
			{
				retval = false;
			}
			val = getRequiredProperty(prefix + "log_level");
			if (val != null) 
			{
					//TODO:Check.This.One.Later
					if(val.Equals("INFO"))
						applicationLoggingLevel = (TraceLevel)3 ; //TODO:How can we do this? Milind
					//Currently info value is 3
			}
			else 
			{
				retval = false;
			}
			if (checkThroughput) 
			{ // Capacity needs criterion for minimum BTPS
				val = getRequiredProperty(prefix + "min_capacity_ratio");
				if (val != null) 
				{
					minBTPSRatio = float.Parse(val) ;//Float.valueOf(val).floatValue();
				}
				else 
				{
					retval = false;
				}
			}
			val = getOptionalProperty(prefix + "warehouse_population");
			if (val != null) 
			{
				warehousePopulationBase = int.Parse(val);//Integer.parseInt(val);
			}
			else 
			{
				warehousePopulationBase = 60;
                        lock (PropertiesForBatch)
                        {
				    PropertiesForBatch.Add(prefix + "warehouse_population", "60");
                        }
			}
			val = getOptionalProperty(prefix + "orderlines_per_order");
			if (val != null) 
			{
				orderlinesPerOrder = int.Parse(val);//Integer.parseInt(val);
			}
			else 
			{
				orderlinesPerOrder = 10;
                        lock (PropertiesForBatch)
                        {
				    PropertiesForBatch.Add(prefix + "orderlines_per_order", "10");
                        }
			}
			val = getRequiredProperty(prefix + "ramp_up_seconds");
			if (val != null) 
			{
				rampupSeconds = int.Parse(val);//Integer.parseInt(val);
			}
			else 
			{
				retval = false;
			}
			val = getRequiredProperty(prefix + "jvm_instances");
			if (val != null) 
			{
				jvm_instances = int.Parse(val);//Integer.parseInt(val);
			}
			else 
			{
				retval = false;
			}
			val = getRequiredProperty(prefix + "per_jvm_warehouse_rampup");
			if (val != null) 
			{
				per_jvm_warehouse_rampup = float.Parse(val);//Float.parseFloat(val);
			}
			else 
			{
				retval = false;
			}
			val = getRequiredProperty(prefix + "per_jvm_warehouse_rampdown");
			if (val != null) 
			{
				per_jvm_warehouse_rampdown = float.Parse(val);//Float.parseFloat(val);
			}
			else 
			{
				retval = false;
			}
			val = getRequiredProperty(prefix + "measurement_seconds");
			if (val != null) 
			{
				measurementSeconds = int.Parse(val);//Integer.parseInt(val);
			}
			else 
			{
				retval = false;
			}
			val = getOptionalProperty(prefix + "expected_peak_warehouse");
            // val = "4";
			if (val != null) 
			{
				tmpValue = int.Parse(val);//Integer.parseInt(val);
				if (tmpValue == 0) 
				{
					expectedPeakWarehouse = int.Parse(Environment.GetEnvironmentVariable("NUMBER_OF_PROCESSORS")) ;
					/*
					tmpValue = Runtime.getRuntime().availableProcessors();
					if (jvm_instances > 1 && tmpValue > 1)
						expectedPeakWarehouse = Runtime.getRuntime()
							.availableProcessors()
							/ jvm_instances;
					else
						expectedPeakWarehouse = Runtime.getRuntime()
							.availableProcessors();*/
				}
				else
				{
                    // UBUNTU_CODE				
					// if(tmpValue == int.Parse(Environment.GetEnvironmentVariable("NUMBER_OF_PROCESSORS")))//Runtime.getRuntime().availableProcessors())
					{
						Console.WriteLine("Warning: Explicitly setting " + prefix + "expected_peak_warehouse");
						Console.WriteLine("requires submission and review by SPEC in order to publish the result.");
					}
					expectedPeakWarehouse = tmpValue;
				}
			}
			else 
			{
                expectedPeakWarehouse = int.Parse(Environment.GetEnvironmentVariable("NUMBER_OF_PROCESSORS")) ;//Runtime.getRuntime().availableProcessors();
                

				/*if (jvm_instances > 1 && tmpValue > 1)
					expectedPeakWarehouse = Runtime.getRuntime()
						.availableProcessors()
						/ jvm_instances;
				else
					expectedPeakWarehouse = Runtime.getRuntime()
						.availableProcessors();*/
			}
                //   lock (PropertiesForBatch)
                //   {
                //       Console.WriteLine("In Lock");
			    // // PropertiesForBatch.Add(prefix + "expected_peak_warehouse", expectedPeakWarehouse.ToString());
                //   }

			val = getRequiredProperty(prefix + "deterministic_random_seed");
			if (val != null) 
			{
				deterministicRandomSeed = bool.Parse(val);//Boolean.parseBoolean(val);
			}
			else 
			{
				retval = false;
			}
			val = getOptionalProperty(prefix + "wait_time_percent");
			if (val != null) 
			{
				waitTimePercent = int.Parse(val);//Integer.parseInt(val);
			}
			else 
			{
				waitTimePercent = 0;
                        lock (PropertiesForBatch)
                        {
				    PropertiesForBatch.Add(prefix + "wait_time_percent", "0");
                        }
			}
			val = getOptionalProperty(prefix + "screen_write");
			if (val != null) 
			{
				if (val.Equals("true")) 
				{
					screenWriteFlag = true;
				}
				else if (val.Equals("false")) 
				{
					screenWriteFlag = false;
				}
				else 
				{
					Console.WriteLine("ERROR:  Property file error");
					Console.WriteLine("   screen_write must be 'true' or 'false'");
					retval = false;
				}
			}
			else 
			{
				screenWriteFlag = false;
                        lock (PropertiesForBatch)
                        {
				    PropertiesForBatch.Add(prefix + "screen_write", "false");
                        }
			}
			val = getOptionalProperty(prefix + "steady_state");
			if (val != null) 
			{
				if (val.Equals("true")) 
				{
					steadyStateFlag = true;
				}
				else if (val.Equals("false")) 
				{
					steadyStateFlag = false;
				}
				else 
				{
					Console.WriteLine("ERROR:  Property file error");
					Console.WriteLine("   steady_state must be 'true' or 'false'");
					retval = false;
				}
			}
			else 
			{
				steadyStateFlag = true;
                        lock (PropertiesForBatch)
                        {
				    PropertiesForBatch.Add(prefix + "steady_state", "true");
                        }
			}
			val = getOptionalProperty(prefix + "override_itemtable_size");
			if (val != null) 
			{
				overrideItemTableSize = int.Parse(val);//Integer.parseInt(val);
			}
			val = getOptionalProperty(prefix + "uniform_random_items");
			if (val != null) 
			{
				if (val.Equals("true")) 
				{
					uniformRandomItems = true;
				}
				else if (val.Equals("false")) 
				{
					uniformRandomItems = false;
				}
				else 
				{
					Console.WriteLine("ERROR:  Property file error");
					Console.WriteLine("   uniform_random_items must be true or false");
					retval = false;
				}
			}
			val = getOptionalProperty(prefix + "starting_number_warehouses");
			if (val != null) 
			{
				startingNumberWarehouses = int.Parse(val);//Integer.parseInt(val);
			}
			val = getOptionalProperty(prefix + "increment_number_warehouses");
			if (val != null) 
			{
				incrementNumberWarehouses = int.Parse(val);//Integer.parseInt(val);
			}
			val = getOptionalProperty(prefix + "ending_number_warehouses");
			if (val != null) 
			{
				endingNumberWarehouses = int.Parse(val);//Integer.parseInt(val);
			}
			else 
			{
				endingNumberWarehouses = expectedPeakWarehouse * 2;
				if (endingNumberWarehouses < 8)
					endingNumberWarehouses = 8;
                        lock (PropertiesForBatch)
                        {
				    PropertiesForBatch.Add(prefix + "ending_number_warehouses", endingNumberWarehouses.ToString());
                        }
			}
			if ((startingNumberWarehouses > 0)
				&& (endingNumberWarehouses >= startingNumberWarehouses)
				&& (incrementNumberWarehouses > 0)) 
			{
				valid_warehouse_sequence = true;
			}
			val = getOptionalProperty(prefix + "sequence_of_number_of_warehouses");
			if (val != null) 
			{
				if (valid_warehouse_sequence) 
				{
					Console.WriteLine("ERROR:  Property file error");
					Console.WriteLine("   Cannot specify both sequence_of_number_of_warehouses and {starting,ending,increment}_number_warehouses");
					retval = false;
				}
				else 
				{
					int startIndex = 0;
					int nextSpace;
					int numEntries = 0;
					while ((nextSpace = val.IndexOf(' ', startIndex)) > 0) 
					{
						numEntries++;
						startIndex = nextSpace + 1;
					}
					numEntries++; // The one at the end, with no space after it.
					sequenceOfWarehouses = new int[numEntries];
					startIndex = 0;
					numEntries = 0;
					while ((nextSpace = val.IndexOf(' ', startIndex)) > 0) 
					{
                        //sequenceOfWarehouses[numEntries] = int.Parse(val.Substring(startIndex, val.Length-nextSpace));//MPH::Aug-30.Integer.parseInt(val.substring(startIndex, nextSpace));
                        sequenceOfWarehouses[numEntries] = int.Parse(val.Substring(startIndex, nextSpace-startIndex)); //MPH:Oct 14
						numEntries++;
						startIndex = nextSpace + 1;
					}
					//sequenceOfWarehouses[numEntries] = int.Parse(val.Substring(startIndex, val.Length));//Integer.parseInt(val.substring(startIndex, val.length()));
                    sequenceOfWarehouses[numEntries] = int.Parse(val.Substring(startIndex, val.Length-startIndex));//MPH Oct 14th
					numEntries++;
					valid_warehouse_sequence = true;
					for (int i = 1; i < numEntries; i++) 
					{
						if (sequenceOfWarehouses[i] < sequenceOfWarehouses[i - 1]) 
						{
							valid_warehouse_sequence = false;
							break;
						}
					}
				}
			}
			val = getOptionalProperty(prefix + "print_properties_and_args");
			if (val != null) 
			{
				if (val.Equals("true")) 
				{
					printPropertiesAndArgs = true;
				}
				else if (val.Equals("false")) 
				{
					printPropertiesAndArgs = false;
				}
				else 
				{
					Console.WriteLine("ERROR:  Property file error");
					Console.WriteLine("   print_properties_and_args must be true or false");
					retval = false;
				}
			}
			Console.WriteLine("\nInput Properties:");
			Console.WriteLine("  per_jvm_warehouse_rampup = "
					   + per_jvm_warehouse_rampup);
			Console.WriteLine("  per_jvm_warehouse_rampdown = "
					   + per_jvm_warehouse_rampdown);
			Console.WriteLine("  jvm_instances = " + jvm_instances);
			Console.WriteLine("  deterministic_random_seed = "
					   + deterministicRandomSeed);
			Console.WriteLine("  ramp_up_seconds = " + rampupSeconds);
			Console.WriteLine("  measurement_seconds = " + measurementSeconds);
			// System.out.println("wait_time_percent = " + waitTimePercent);
			if (checkThroughput) 
			{
				// System.out.println("min_capacity_ratio = " + minBTPSRatio);
			}
			// System.out.println(" forcegc = " + forceGC);
			// System.out.println("steady_state = " + steadyStateFlag);
			// System.out.println("screen_write = " + screenWriteFlag);
			if (sequenceOfWarehouses == null) 
			{
				Console.WriteLine("  starting_number_warehouses = "
						   + startingNumberWarehouses);
				Console.WriteLine("  increment_number_warehouses = "
						   + incrementNumberWarehouses);
				Console.WriteLine("  ending_number_warehouses = "
						   + endingNumberWarehouses);
				Console.WriteLine("  expected_peak_warehouse = "
						   + expectedPeakWarehouse);
			}
			else 
			{
				Console.Write("  sequence_of_number_of_warehouses = ");
				for (int i = 0; i < sequenceOfWarehouses.Length; i++) 
				{
					Console.Write(" " + sequenceOfWarehouses[i]);
				}
				Console.WriteLine("  expected_peak_warehouse = "
						   + expectedPeakWarehouse);
				Console.WriteLine("");
			}
			if (!valid_warehouse_sequence) 
			{
				Console.WriteLine("ERROR:  Property file error");
				Console.WriteLine("   No valid warehouse sequence specified.");
			}
			if (overrideItemTableSize > 0) 
			{
				// System.out.println("Item table size overridden to: " +
				// overrideItemTableSize);
			}
			// System.out.println("uniform_random_items = " + uniformRandomItems);
			// System.out.println("print_properties_and_args = " +
			// printPropertiesAndArgs);
			return retval && valid_warehouse_sequence;
		}//getProps

		public bool copyPropsToOutput(StreamWriter outRawFile) 
		{
			bool retval = true;
            return retval;
            
			String[] input_props = {
									   "suite", "log_level", "warehouse_population",
									   "orderlines_per_order", "ramp_up_seconds",
									   "measurement_seconds", "wait_time_percent", "screen_write",
									   "steady_state", "starting_number_warehouses",
									   "increment_number_warehouses", "ending_number_warehouses",
									   "sequence_of_number_of_warehouses", "include_file",
									   "override_itemtable_size", "uniform_random_items",
									   "print_properties_and_args", "output_directory",
									   "expected_peak_warehouse", "deterministic_random_seed",
									   "jvm_instances", "per_jvm_warehouse_rampup",
									   "per_jvm_warehouse_rampdown"
								   };
			int num_props = 0;
			//for (Enumeration e = PropertiesForBatch.propertyNames(); e.hasMoreElements();) 
                  lock (PropertiesForBatch)
                  {
			  for(IEnumerator e = PropertiesForBatch.Keys.GetEnumerator();e.MoveNext();)
			  {
				String name = (String) e.Current ; //nextElement();
				if (name.StartsWith("config.")) 
				{
					num_props++;
				}
				else if (name.StartsWith("input.")) 
				{
					bool found = false;
					for (int i = 0; i < input_props.Length; i++) 
					{
                        if(name == "input.expected_peak_warehouse")
                        {
                            Console.WriteLine("###" + input_props[i]);
                        }
						if (name.Equals("input." + input_props[i])) 
						{
							found = true;
							break;
						}
					}
					if (found) 
					{
						num_props++;
					}
					else if (name.Equals("input.min_capacity_ratio")) 
					{
						if (checkThroughput) 
						{
							num_props++;
						}
						else 
						{
							Console.WriteLine("ERROR:  Property file error");
							Console.WriteLine("   input.min_capacity_ratio invalid for input.suite=SPECjbb");
							retval = false;
						}
					}
					else if (name.Equals("input.show_warehouse_detail")) 
					{
						if (((String)PropertiesForBatch["input.show_warehouse_detail"]).Equals("true"))
							showWarehouseDetail = true;
						num_props++;
					}
					else 
					{
						Console.WriteLine("**ERROR:  Property file error");
						Console.WriteLine("   Unrecognized property:  " + name);
						retval = false;
					}
				}
				else 
				{
					// Set return value to false, but keep processing so user can
					// correct all spelling errors in a single pass.
					Console.WriteLine("*ERROR:  Property file error");
					Console.WriteLine("   Unrecognized property:  " + name);
					retval = false;
				}
			  }
                  }//lock
			if (retval) 
			{
				//Vector keyvec = new Vector(PropertiesForBatch.keySet());
				ArrayList keyvec = ArrayList.Synchronized(new ArrayList(PropertiesForBatch.Keys));
				keyvec.Sort();//Collections.sort(keyvec);
				for (int i = 0; i < keyvec.Count; i++) 
				{
					String propsKey = (String) keyvec[i]; //elementAt(i);
					String svalue = (String)PropertiesForBatch[propsKey];
					outRawFile.WriteLine(propsKey + "=" + svalue);
				}
				//Integer procsAvail = new Integer(Runtime.getRuntime().availableProcessors());
				int procsAvail = int.Parse((Environment.GetEnvironmentVariable("NUMBER_OF_PROCESSORS")));
				outRawFile.WriteLine("config.sw.procsAvailtoJava" + "="+ procsAvail.ToString());
			}
			return retval;
		}//copyPropsToOutput

		public String setProp(String prop, String value1) 
		{
			PropertiesForBatch.Add(prop, value1);
			return (String)PropertiesForBatch[prop];
		}

		private bool checkCompliance(int actualValue, int compliantValue, String name) 
		{
			// System.out.println(name + " " + actualValue + " " + compliantValue);
			if (actualValue != compliantValue) 
			{
				Console.WriteLine("INVALID:  " + name + " = " + actualValue + ", must be " + compliantValue);
				return false;
			}
			return true;
		}

		private bool checkCompliance(bool actualValue, bool compliantValue, String name) 
		{
			// System.out.println(name + " " + actualValue + " " + compliantValue);
			if (actualValue != compliantValue) 
			{
				Console.WriteLine("INVALID:  " + name + " = " + actualValue
						   + ", must be " + compliantValue);
				return false;
			}
			return true;
		}

		private bool checkCompliance(float actualValue, float compliantValue,
			String name) 
		{
			// System.out.println(name + " " + actualValue + " " + compliantValue);
			if (actualValue != compliantValue) 
			{
				Console.WriteLine("INVALID:  " + name + " = " + actualValue
						   + ", must be " + compliantValue);
				return false;
			}
			return true;
		}

		
		private bool checkCompliance(TraceLevel actualValue, TraceLevel compliantValue, String name) 
		{
			// System.out.println(name + " " + actualValue + " " + compliantValue);
			if (actualValue.Equals(compliantValue) == false) 
			{
				Console.WriteLine("INVALID:  " + name + " = " + actualValue
						   + ", must be " + compliantValue);
				return false;
			}
			return true;
		}

		public void checkCompliance() 
		{
			bool compliant = true;
			Console.WriteLine("\n\nChecking whether run will be valid");
			if (checkThroughput) 
			{ // Capacity
			}
			else 
			{ // Rate
				compliant = checkCompliance(warehousePopulationBase,
					COMPLIANT_RATE_warehousePopulationBase,
					"warehouse_population_base")
					&& compliant;
				compliant = checkCompliance(orderlinesPerOrder,
					COMPLIANT_RATE_orderlinesPerOrder, "orderlines_per_order")
					&& compliant;
				compliant = checkCompliance(deterministicRandomSeed,
					COMPLIANT_RATE_deterministicRandomSeed,
					"deterministic_random_seed")
					&& compliant;
				compliant = checkCompliance(per_jvm_warehouse_rampup,
					COMPLIANT_RATE_per_jvm_warehouse_rampup,
					"per_jvm_warehouse_rampup")
					&& compliant;
				compliant = checkCompliance(per_jvm_warehouse_rampdown,
					COMPLIANT_RATE_per_jvm_warehouse_rampdown,
					"per_jvm_warehouse_rampdown")
					&& compliant;
				compliant = checkCompliance(rampupSeconds,
					COMPLIANT_RATE_rampupSeconds, "ramp_up_seconds")
					&& compliant;
				compliant = checkCompliance(measurementSeconds,
					COMPLIANT_RATE_measurementSeconds, "measurement_seconds")
					&& compliant;
				compliant = checkCompliance(waitTimePercent,
					COMPLIANT_RATE_waitTimePercent, "wait_time_percent")
					&& compliant;
				compliant = checkCompliance(steadyStateFlag,
					COMPLIANT_RATE_steadyStateFlag, "steady_state")
					&& compliant;
				compliant = checkCompliance(screenWriteFlag,
					COMPLIANT_RATE_screenWriteFlag, "screen_write")
					&& compliant;
				compliant = checkCompliance(uniformRandomItems,
					COMPLIANT_RATE_uniformRandomItems, "uniform_random_items")
					&& compliant;
				compliant = checkCompliance(applicationLoggingLevel,
					COMPLIANT_RATE_applicationLoggingLevel, "log_level")
					&& compliant;
				if (overrideItemTableSize > 0) 
				{
					compliant = checkCompliance(overrideItemTableSize,
						COMPLIANT_RATE_overrideItemTableSize,
						"override_itemtable_size")
						&& compliant;
				}
			}
			if (compliant) 
			{
				Console.WriteLine();
				Console.WriteLine("Run will be COMPLIANT");
				Console.WriteLine();
			}
			else 
			{
				Console.WriteLine();
				Console.WriteLine("INVALID:  Run will NOT be compliant");
				Console.WriteLine();
			}
		}

		public TraceLevel getApplicationLoggingLevel() 
		{
			return applicationLoggingLevel;
		}

	}
}