<%@ Language="VBScript" %>
<!-- #include file="includes/db.inc" -->
<!-- #include virtual="/includes/header.inc" -->
<%
Dim invoiceId, invoice, conn, cmd, rs, exporter, sql
invoiceId = CLng(Request.QueryString("id"))

' The billing rules live in the COM library.
Set invoice = Server.CreateObject("AcmeBilling.Invoice")
invoice.Load invoiceId

' The other invoices of the same customer come straight from the database.
Set conn = OpenBilling()
Set cmd = Server.CreateObject("ADODB.Command")
Set cmd.ActiveConnection = conn
cmd.CommandText = "dbo.usp_GetCustomerInvoices"
cmd.CommandType = adCmdStoredProc
cmd.Parameters.Append cmd.CreateParameter("@CustomerId", adInteger, adParamInput, , invoice.CustomerId)
Set rs = cmd.Execute

' Which exporter runs is a per-customer setting.
If Request.QueryString("export") <> "" Then
    Set exporter = Server.CreateObject("AcmeExport." & Request.QueryString("export"))
    exporter.Send invoice
    sql = "UPDATE dbo.Invoices SET ExportedOn = GETDATE() WHERE InvoiceId = " & invoiceId
    conn.Execute sql
End If
%>
<h1>Invoice <%= invoiceId %></h1>
<p>Total due: <%= invoice.TotalDue %></p>
<h2>Other invoices for this customer</h2>
<ul>
<% Do Until rs.EOF %>
  <li><%= rs("InvoiceId") %>: <%= rs("Total") %></li>
<%   rs.MoveNext
   Loop
   conn.Close %>
</ul>
</body>
</html>
