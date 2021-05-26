using System;
using System.Runtime.CompilerServices;

namespace Specjbb2005.src.spec.jbb
{
	/// <summary>
	/// Summary description for Address.
	/// </summary>
	public class Address
	{

		// This goes right after each class/interface statement
		//  internal static readonly String COPYRIGHT = "SPECjbb2005,"
		//  + "Copyright (c) 2000-2005 Standard Performance Evaluation Corporation (SPEC),"
		//  + "All rights reserved,"
		//  + "(C) Copyright IBM Corp., 1996 - 2005"
		//  + "All rights reserved,"
		//  + "Licensed Materials - Property of SPEC";

		private String      street1;

		private String      street2;

		private String      city;

		private String      state;

		private String      zip;

        private readonly object _syncRoot = new Object();

        public Address()
		{
			
		}
		
		// CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
		public void setUsingRandom(Random r) 
		{
            lock (_syncRoot)
            {

                street1 = new String(JBButil.create_random_a_string(10, 20, r));
                street1 = new String(JBButil.create_random_a_string(10, 20, r));
                street2 = new String(JBButil.create_random_a_string(10, 20, r));
                city = new String(JBButil.create_random_a_string(10, 20, r));
                state = new String(JBButil.create_random_a_string(2, 2, r));
                zip = new String(JBButil.create_random_n_string(9, 9, r));
            }
		}//setUsingRandom

		// CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
		public void setUsingRandom(short warehouseId)
        {
            lock (_syncRoot)
            {
                street1 = new String(JBButil.create_random_a_string(10, 20, warehouseId));
                street1 = new String(JBButil.create_random_a_string(10, 20, warehouseId));
                street2 = new String(JBButil.create_random_a_string(10, 20, warehouseId));
                city = new String(JBButil.create_random_a_string(10, 20, warehouseId));
                state = new String(JBButil.create_random_a_string(2, 2, warehouseId));
                zip = new String(JBButil.create_random_n_string(9, 9, warehouseId));
            }
		}//setUsingRandom
		
		// CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
		public void display() 
		{
            lock (_syncRoot)
            {
                Console.WriteLine("****** ADDRESS display *********************");
                Console.WriteLine("     Street 1 is  " + street1);
                Console.WriteLine("     Street 2 is  " + street2);
                Console.WriteLine("    City name is  " + city);
                Console.WriteLine("   State name is  " + state);
                Console.WriteLine("     Zip code is  " + zip);
                Console.WriteLine("********************************************");
            }
		}//display

		// CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
		public String getStreet1() 
		{
            lock (_syncRoot)
            {
                String temp = street1;
                return temp;
            }
		}//getStreet1

		// CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
		public String getStreet2() 
		{
            lock (_syncRoot)
            {
                String temp = street2;
                return temp;
            }
		}//getStreet2

		// CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
		public String getCity() 
		{
            lock (_syncRoot)
            {
                String temp = city;
                return temp;
            }
		}//getCity

		// CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
		public String getState() 
		{
            lock (_syncRoot)
            {
                String temp = state;
                return temp;
            }
		}//getState

		// CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
		public String getZip() 
		{
            lock (_syncRoot)
            {
                String temp = zip;
                return temp;
            }
		}//getZip
	}
}
