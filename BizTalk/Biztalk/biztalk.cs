/*
 * biztalk.cs : 
 * Copyright (c) 2000, Microsoft Corporation. All Rights Reserved.
 */
#define DUMP_TREE

namespace PerfTest.BizTalk {

using System;
using System.IO;
using System.Text;
using System.Data;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Xsl;
using System.Xml.Schema;
using System.Runtime.InteropServices;

public class CmdLine {
    public bool    UseSchemaCache = false;
    public bool    UseXslTemplate = true;
    public bool    DumpTreeBuidInst = false;
    public bool    Help = false;
    public String  Error = "";
    public String  XmlFile = "";

    private Char[] parametrPrefixes = {'-', '/'};

    public CmdLine(String[] args) {
        foreach(String arg in args) {
            if("" != arg) {
                if(0 == arg.IndexOf(parametrPrefixes[0]) && 0 == arg.IndexOf(parametrPrefixes[1])) {
                    ParceArgument(arg.Substring(1,arg.Length - 1), /*parametr:*/ true);
                }else {
                    ParceArgument(arg, /*parametr:*/ false);
                }
            }
        }
    }// CmdLine

    private void ParceArgument(String arg, bool parametr) {
        if(parametr) {
            switch(String.Intern(arg)) {
            case "?" : 
                Help = true; 
                break;            
            case "S" : 
            case "s" : 
                UseSchemaCache = false;
                break;
            case "T" : 
            case "t" : 
                UseXslTemplate = false;
                break;
            case "B" : 
            case "b" : 
                DumpTreeBuidInst = true;
                break;
            default :
                Error  = "Unknown argument: '/" + arg + "'";
                break;
            }
        }
    }// ParceArgument

    public void PrintHelp() {
        Console.Error.WriteLine("Biztalk [-S] [-T] ");
        Console.Error.WriteLine("parametrs:");
        Console.Error.WriteLine("\t-?             - This Help");
        Console.Error.WriteLine("\t-S             - Dont use Schema cache  (default: Don't use)");
        Console.Error.WriteLine("\t-T             - Dont XSL Template (default: Use)");
        Console.Error.WriteLine("\t-b             - DumpTreeBuidInst (default: no)");
    }// PrintHelp
};// CmdLine

public class NullTextWriter : TextWriter {
    public override string NewLine {
        get { return ""; }
        set {}
    }
    public override void Close() {}
    public override void Flush() {}
    public override void Write (String format, Object[] arg)  {}
    public override void Write (String format, Object arg0, Object arg1, Object arg2) {}
    public override void Write (String format, Object arg0, Object arg1)  {}
    public override void Write (String format, Object arg0)  {}
    public override void Write (Object value)  {}
    public override void Write (String value)  {}
    public override void Write (Double value)  {}
    public override void Write (Single value)  {}
    //public override void Write (ulong value)  {}
    public override void Write (Int64 value)  {}
    //public override void Write (uint value)  {}
    public override void Write (Int32 value)  {}
    public override void Write (Boolean value)  {}
    public override void Write (Char[] buffer, Int32 index, Int32 count)  {}
    public override void Write (Char[] buffer)  {}
    public override void Write (Char value)  {}
    public override void WriteLine (String format, Object[] arg)  {}
    public override void WriteLine (String format, Object arg0, Object arg1, Object arg2)  {}
    public override void WriteLine (String format, Object arg0, Object arg1)  {}
    public override void WriteLine (String format, Object arg0)  {}
    public override void WriteLine (Object value)  {}
    public override void WriteLine (String value)  {}
    public override void WriteLine (Double value)  {}
    public override void WriteLine (Single value)  {}
    //public override void WriteLine (uint value)  {}
    public override void WriteLine (Int64 value)  {}
    public override void WriteLine (Int32 value)  {}
    public override void WriteLine (Boolean value)  {}
    public override void WriteLine (Char[] buffer, Int32 index, Int32 count)  {}
    public override void WriteLine (Char[] buffer)  {}
    public override void WriteLine (Char value)  {}
    public override void WriteLine ( )  {}

    public override Encoding Encoding {
        get { return Encoding.UTF8; }
    }
}

public class DocBuilder {
//  To build the instruction sequence for building a document, 
//  just build this as an exe, and then run 
//      > biztalk -b t.xml
//  where t.xml is the sample file you want built.
//  This will dump to stdout a sequence of TreeBuilderInstr-s
//  which should build that document.
    public enum CmdEnum {
        BUILD_STARTELEMENT,
        BUILD_ENDELEMENT  ,
        BUILD_ATTRIBUTE   ,
        BUILD_FINISH,
    };
    public struct BldInstr {
        public CmdEnum   Cmd;
        public String    Name;
        public String    Text;
        public BldInstr( CmdEnum cmd, String name, String text) { 
            Cmd  = cmd;
            Name = name;
            Text = text;
        }
    };

    public static void DumpElem( XmlNode node ) {
        Console.WriteLine( "    { BUILD_STARTELEMENT, L\"" + node.Name + "\", NULL},\n" );
        if ( node.Attributes.Count > 0 ) {
            foreach (XmlAttribute attr in node.Attributes ) {
                Console.WriteLine( "    { BUILD_ATTRIBUTE, L\"" + attr.Name + "\", L\"" + attr.Value + "\"},\n");
            }
        }
    }
    
    public static void DumpTreeBuidInst(String xmlFile) {
        if(null == xmlFile || "" == xmlFile) {
            Console.Error.WriteLine("No Xml file was specified");
            return;
        }
        XmlTextReader xmlReader = new XmlTextReader(xmlFile);
        XmlDocument doc = new XmlDocument();
        doc.Load( xmlFile );
        DumpElem( doc.DocumentElement );
    }// DumpTreeBuidInst

    private static BldInstr new_BldInstr(CmdEnum cmd, String name, String text) {
        return new BldInstr( cmd, name, text);
    }

    // ------------------ Particular Tree ------------

    public static XmlDocument BuildXmlData0() {
        XmlDocument doc = new XmlDocument();
        XmlNode curNode = doc;
        int i = 0;
        while ( xmldata0[i].Cmd != CmdEnum.BUILD_FINISH ) {
            switch ( xmldata0[i].Cmd ) {
                case CmdEnum.BUILD_STARTELEMENT: {
                    XmlElement newNode = doc.CreateElement( xmldata0[i].Name );
                    curNode.AppendChild( (XmlNode)newNode );
                    curNode = newNode;
                    break;
                }
                case CmdEnum.BUILD_ATTRIBUTE : {
                    if ( curNode.NodeType == XmlNodeType.Element ) {
                        XmlAttribute attr = doc.CreateAttribute( xmldata0[i].Name );
                        attr.Value = xmldata0[i].Text;
                        ((XmlElement)curNode).SetAttributeNode( attr );
                    }
                    break;
                }
                case CmdEnum.BUILD_ENDELEMENT : {
                    curNode = curNode.ParentNode;
                    break;
                }
                default:
                    break;
            }
            i++;
        }        
        return doc; // BuildTree(xmldata0);
    }

    private static BldInstr[] xmldata0 = {
        new_BldInstr( CmdEnum.BUILD_STARTELEMENT, "PurchaseOrder", null),
        new_BldInstr( CmdEnum.BUILD_STARTELEMENT, "POHeader"     , null),
        new_BldInstr( CmdEnum.BUILD_ATTRIBUTE   , "Purpose"      , "Purpose A"),
        new_BldInstr( CmdEnum.BUILD_ATTRIBUTE   , "Type"         , "Type A"),
        new_BldInstr( CmdEnum.BUILD_ATTRIBUTE   , "Number"       , "10"),
        new_BldInstr( CmdEnum.BUILD_ATTRIBUTE   , "CreationDate" , "1999-01-01"),
        new_BldInstr( CmdEnum.BUILD_ENDELEMENT  , "POHeader"     , null),
        new_BldInstr( CmdEnum.BUILD_STARTELEMENT, "BillTo"       , null),
        new_BldInstr( CmdEnum.BUILD_STARTELEMENT, "Address"      , null),
        new_BldInstr( CmdEnum.BUILD_ATTRIBUTE   , "Name"         , "Mr Bill1"),
        new_BldInstr( CmdEnum.BUILD_ATTRIBUTE   , "Address1"     , "Bill1 Ave"),
        new_BldInstr( CmdEnum.BUILD_ATTRIBUTE   , "Address2"     , "Apt# bill1"),
        new_BldInstr( CmdEnum.BUILD_ATTRIBUTE   , "City"         , "BillC1"),
        new_BldInstr( CmdEnum.BUILD_ATTRIBUTE   , "State"        , "BillS1"),
        new_BldInstr( CmdEnum.BUILD_ATTRIBUTE   , "PostalCode"   , "B001"),
        new_BldInstr( CmdEnum.BUILD_ATTRIBUTE   , "Country"      , "Bill1"),
        new_BldInstr( CmdEnum.BUILD_ENDELEMENT  , "Address"      , null),
        new_BldInstr( CmdEnum.BUILD_ENDELEMENT  , "BillTo"       , null),
        new_BldInstr( CmdEnum.BUILD_STARTELEMENT, "ShipTo"       , null),
        new_BldInstr( CmdEnum.BUILD_STARTELEMENT, "Address"      , null),
        new_BldInstr( CmdEnum.BUILD_ATTRIBUTE   , "Name"         , "Ms Ship1"),
        new_BldInstr( CmdEnum.BUILD_ATTRIBUTE   , "Address1"     , "Ship1 St"),
        new_BldInstr( CmdEnum.BUILD_ATTRIBUTE   , "Address2"     , "Suite# ship1"),
        new_BldInstr( CmdEnum.BUILD_ATTRIBUTE   , "City"         , "ShipC1"),
        new_BldInstr( CmdEnum.BUILD_ATTRIBUTE   , "State"        , "ShipS1"),
        new_BldInstr( CmdEnum.BUILD_ATTRIBUTE   , "PostalCode"   , "S001"),
        new_BldInstr( CmdEnum.BUILD_ATTRIBUTE   , "Country"      , "Ship1"),
        new_BldInstr( CmdEnum.BUILD_ENDELEMENT  , "Address"      , null),
        new_BldInstr( CmdEnum.BUILD_ENDELEMENT  , "ShipTo"       , null),
        new_BldInstr( CmdEnum.BUILD_STARTELEMENT, "Item"         , null),
        new_BldInstr( CmdEnum.BUILD_STARTELEMENT, "ItemHeader"   , null),
        new_BldInstr( CmdEnum.BUILD_ATTRIBUTE   , "LineNumber"   , "001"),
        new_BldInstr( CmdEnum.BUILD_ATTRIBUTE   , "ItemNumber"   , "001"),
        new_BldInstr( CmdEnum.BUILD_ATTRIBUTE   , "Description"  , "description item 1"),
        new_BldInstr( CmdEnum.BUILD_ATTRIBUTE   , "Quantity"     , "1"),
        new_BldInstr( CmdEnum.BUILD_ATTRIBUTE   , "Price"        , "1"),
        new_BldInstr( CmdEnum.BUILD_ATTRIBUTE   , "UnitOfMeasure", "item1"),
        new_BldInstr( CmdEnum.BUILD_ATTRIBUTE   , "ExtendedPrice", "1"),
        new_BldInstr( CmdEnum.BUILD_ATTRIBUTE   , "BuyerPart"    , "Buyer item1"),
        new_BldInstr( CmdEnum.BUILD_ATTRIBUTE   , "VendorPart"   , "Vendor item 1"),
        new_BldInstr( CmdEnum.BUILD_ATTRIBUTE   , "UPC"          , "UPC1"),
        new_BldInstr( CmdEnum.BUILD_ENDELEMENT  , "ItemHeader"   , null),
        new_BldInstr( CmdEnum.BUILD_ENDELEMENT  , "Item"         , null),
        new_BldInstr( CmdEnum.BUILD_ENDELEMENT  , "PurchaseOrder", null),
        new_BldInstr( CmdEnum.BUILD_FINISH      , null           , null),
    };
};// DocBuilder

public class Biztalk {
    protected CmdLine      cmdLine;
    protected bool         Scenario2 = false;
    protected XslTransform xsltTemplate = null;
    XmlDocument pSchemaDoc = null;
    XmlSchemaCollection    g_rgpSchemas = null;

    public void Setup( params object[] args ) {
        InitProc( (string [])args);
    }

    public void Prepare() {
    //empty
    }

    public void Cleanup() {
    //empty
    }
    
    public void Run() {
        if(cmdLine.DumpTreeBuidInst) {
            DocBuilder.DumpTreeBuidInst(cmdLine.XmlFile);
        }else {
            TransactionProc();
        }
    }// Run


    private XslTransform LoadXsl(String fileName) {
        XslTransform xslt = new XslTransform(); {
            xslt.Load(fileName);
        }
        return xslt;
    }// LoadXsl

    private bool InitProcReal() {
        if(cmdLine.UseSchemaCache) {
            // no validation is supported in this test
            g_rgpSchemas = new XmlSchemaCollection();
            pSchemaDoc = new XmlDocument();
            pSchemaDoc.Load("b2schemas.xml");
        }
        // 2. Load XslTemplate
        if(cmdLine.UseXslTemplate) {
            //xsltTemplate = LoadXsl(Scenario2 ? "b2mapping.xsl" : "c:\\PerfHarnessManaged\\biztalk.xsl");
	    //xsltTemplate = LoadXsl(Scenario2 ? "b2mapping.xsl" : "C:\\BMRoot\\biztalk\\biztalk.xsl");
		
	    xsltTemplate = LoadXsl(Scenario2 ? "b2mapping.xsl" : ".\\biztalk.xsl");
        }

        return true;
    }// InitProcReal

    public bool InitProc(String[] prms) {
        cmdLine = new CmdLine(prms);
        return InitProcReal();
    }// InitProc

    public void ExitProc() {
    }// ExitProc

    public bool TransactionProc() {
        if (Scenario2)
            return false;
        else
            return TransactionProc1();
    }// TransactionProc

    private XmlDocument Transform(XPathDocument xmlDoc) {
        XslTransform xslt = cmdLine.UseXslTemplate ? xsltTemplate : LoadXsl(Scenario2 ? "b2mapping.xsl" : "c:\\PerfHarnessManaged\\biztalk.xsl");
        XmlDocument res = new XmlDocument();
        XsltArgumentList argList = null;
        res.Load(xslt.Transform( xmlDoc, /*args:*/argList));
        return res;
    }// Transform

    private bool TransactionProc1(/*DWORD dwThreadID*/) {
        XmlDocument xmlDoc = DocBuilder.BuildXmlData0();
        String strXml = xmlDoc.OuterXml;
        XPathDocument xmlDoc2 = new XPathDocument( new StringReader( strXml ) );
        XmlDocument xmlDoc3 = Transform(xmlDoc2);
        xmlDoc3.Save(new NullTextWriter());

        return true;
    }// TransactionProc1
};// Biztalk
}
