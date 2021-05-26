/*
 * 
 * Copyright (c) 2000-2005 Standard Performance Evaluation Corporation (SPEC) All
 * rights reserved. Copyright (c) 1996-2005 IBM Corporation, Inc. All rights
 * reserved.
 */
using System;
using System.Collections;

namespace Specjbb2005.src.spec.jbb
{
	/// <summary>
	/// Summary description for MapDataStorage.
	/// </summary>
	public class MapDataStorage : JBBDataStorage
	{

		/**
	 * serialversionUID of 1 for first release
	 */
		// private static readonly long serialVersionUID = 1L;

		// This goes right after each class/interface statement
		//  static readonly String       COPYRIGHT        = "SPECjbb2005,"
		//  + "Copyright (c) 2005 Standard Performance Evaluation Corporation (SPEC),"
		//  + "All rights reserved,"
		//  + "(C) Copyright IBM Corp., 1996 - 2005"
		//  + "All rights reserved,"
		//  + "Licensed Materials - Property of SPEC";

		//protected Map             data;
		//protected internal SortedList		data;
		protected internal IDictionary data;

		//protected accesses to package + derived classes.
		protected internal MapDataStorage() : this(new Hashtable())//this(new SortedList())
		{
			//MapDataStorage(new SortedList()) ;//this(new HashMap());
		}

		internal protected MapDataStorage(/*SortedList*/IDictionary data) 
		{
			this.data = data;
		}

		public IEnumerator elements() 
		{
			return data.Values.GetEnumerator() ;//data.values().iterator();
		}

		public bool containsKey(Object key) 
		{
			//return ((Hashtable)data).ContainsKey(key) ; //.containsKey(key); October 20th MPH
            return (data.Contains(key));
		}

		public Object get(Object key) 
		{
			return data[key] ; //.get(key);
		}

        public Object put(Object key, Object value1) 
		{
			Object retVal = null ;
			//retVal = data[key];//Li: no one actually get the prev value, just return null
			data[key] = value1 ;//hashmap returns the previous value associated with the key or null;
			return retVal ;//check this thing. Is there more appropriate port?  
		}

		public Object remove(Object key) 
		{
			Object retVal = null ;
			//retVal = data[key];//Li:return null, let caller to handle synchronization issue
			data.Remove(key);
			return retVal;
		}

		public int size() 
		{
			return data.Count ; //.size();
		}
		
	}
}
