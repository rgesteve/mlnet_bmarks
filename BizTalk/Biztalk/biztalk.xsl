<xsl:stylesheet version='1.0' xmlns:xsl='http://www.w3.org/1999/XSL/Transform'>
<xsl:template match='/'>
	<xsl:apply-templates select='PurchaseOrder'/>
</xsl:template>
<xsl:template match='PurchaseOrder'>
<X12_850_FG>
	<xsl:for-each select='POHeader'>
	<ST>
		<!-- Connection from Source Node 'Purpose' to Destination Node 'ST01' -->
		<xsl:attribute name='ST01'><xsl:value-of select='./@Purpose'/></xsl:attribute>
		<!-- Connection from Source Node 'Name' to Destination Node 'ST02' -->
		<xsl:attribute name='ST02'><xsl:value-of select='../BillTo/Address/@Name'/></xsl:attribute>
	</ST>
	</xsl:for-each>
</X12_850_FG>
</xsl:template>
</xsl:stylesheet>
