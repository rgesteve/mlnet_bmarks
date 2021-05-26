/*
 *
 * Copyright (c) 2005 Standard Performance Evaluation Corporation (SPEC) All
 * rights reserved. 
 *
 * This source code is provided as is, without any express or implied warranty.
 */
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Xml;

namespace Specjbb2005.src.spec.jbb.infra.Util
{
	/// <summary>
	/// Summary description for XMLTransactionLog.
	/// </summary>
	/// 

	public class XMLLineDocumentException : Exception 
	{
									   /**
										* serialversionUID = 1 for first release
										*/
		private const long serialVersionUID = 1L;

		// This goes right after each class/interface statement
		//  internal static readonly  String       COPYRIGHT        = "SPECjbb2005,"
		//  + "Copyright (c) 2005 Standard Performance Evaluation Corporation (SPEC),"
		//  + "All rights reserved,"
		//  + "Licensed Materials - Property of SPEC";

		XMLLineDocumentException() : base()
		{
			//base();
		}

		XMLLineDocumentException(String s):base(s) 
		{
			//base(s);
		}
	};

	public class XMLTransactionLog
	{
		// This goes right after each class/interface statement
		static readonly String     COPYRIGHT = "SPECjbb2005,"
		+ "Copyright (c) 2005 Standard Performance Evaluation Corporation (SPEC),"
		+ "All rights reserved,"
		+ "Licensed Materials - Property of SPEC";

		static XmlDocument         templateDocument = new XmlDocument();

		// static XmlTextReader	   builder;

		static XMLTransactionLog() 
		{
			
			// initialize document
			//DocumentBuilderFactory factory = DocumentBuilderFactory.newInstance();
			try 
			{
                //builder = factory.newDocumentBuilder();
                //templateDocument = builder.parse("xml/template-document.xml");
                // builder = new XmlTextReader(new FileStream("xml\\template-document.xml",
                //FileMode.Open,
                //FileAccess.Read));
                
                TextReader textReader = File.OpenText(@"xml/template-document.xml");
                XmlReaderSettings settings = new XmlReaderSettings();
                settings.IgnoreWhitespace = true;
                settings.DtdProcessing = DtdProcessing.Ignore;


                using (StringReader sr = new StringReader(textReader.ReadToEnd()))
                using (XmlReader reader = XmlReader.Create(sr, settings))
                {
                    templateDocument.Load(reader);
                }


            }
            catch (XmlException sxe) 
			{
				// Error generated during parsing)
				Console.Error.WriteLine("SAX Error in template-document initialization.");
				Exception ex;
				if (sxe.GetBaseException() != null)
					ex = sxe.GetBaseException();
				Console.WriteLine(sxe.StackTrace);
				// CORECLR Environment.Exit(1);
			}/*
			catch (ParserConfigurationException pce) 
			{
				// Parser with specified options can't be built
				System.err
					.println("Parser configuration error in template-document initialization.");
				pce.printStackTrace();
				System.exit(1);
			}*/
			catch (IOException ioe) 
			{
				// I/O error
				Console.WriteLine("I/O error in template-document initialization.");
				Console.WriteLine(ioe.StackTrace);
                // CORECLR Environment.Exit(1);
            }

        }//XMLTransactionLog

		//private ArrayList lineCache = null;//ArrayList<Node> lineCache = null;
        private List<XmlNode> lineCache = null;

		XmlDocument                document;

		public XMLTransactionLog() 
		{
			// create new line cache
            lineCache = new List<XmlNode>(0);//new ArrayList(0);//new ArrayList<Node>(0);
			// copy from template XML document
			copy(templateDocument);
		}

		public void populateXML(TransactionLogBuffer log_buffer) 
		{
			for (int i = 0; i < log_buffer.getLineCount(); i++) 
			{
				putLine(log_buffer.getLine(i), i);
			}
		}

		public void clear() 
		{
			//Element baseElement = document.getDocumentElement();
			XmlElement baseElement = document.DocumentElement;
			XmlNode current_node = baseElement.LastChild;//baseElement.getLastChild();
			XmlNode next_node = null;
			while ((next_node = current_node.PreviousSibling/*getPreviousSibling()*/) != null) 
			{
				XmlNode lineNode = baseElement.RemoveChild(current_node);//removeChild(current_node);
				if (lineNode.Name.Equals("Line")) 
				{
					// set the removed line's LineData Text Value to ""
					lineNode.LastChild.LastChild.Value = "";//.setNodeValue("");
					// add the removed line to the lineCache
					lineCache.Add(lineNode);
				}
				current_node = next_node;
			}
		}

		public void copy(XmlDocument master) 
		{
			// copy the document
			document = /*templateDocument*/(XmlDocument)master.CloneNode(true);//(Document) master.cloneNode(true);
		}

		private void  putLine(String s, int n) 
		{
			int line_number = n;
			XmlNode jbbDocument = document.LastChild;//getLastChild();
			// Check and see if a line element is available
			// in the line cache
			int cacheLength = lineCache.Count;//size();
			if (cacheLength > 0) 
			{
				// fetch a line from the line cache
				XmlNode lineNode = (XmlNode)lineCache[cacheLength-1];//MPH:Remove returns void. So get the element and remove it.
				lineCache.RemoveAt(cacheLength - 1);
				jbbDocument.AppendChild(lineNode);
				// set the TextNode of the LineData child from lineNode
				//lineNode.getLastChild().getLastChild().setNodeValue(s);
				lineNode.LastChild.LastChild.Value = s ;
			}
			else 
			{
				// Create a new line element and append it to the document
				XmlElement lineNode = (XmlElement) document.CreateElement("Line");
				jbbDocument.AppendChild(lineNode);
				XmlElement newData = document.CreateElement("LineData");
				lineNode.AppendChild(newData);
				XmlNode new_node = document.CreateTextNode(s);
				newData.AppendChild(new_node);
			}
		}
	}
}
