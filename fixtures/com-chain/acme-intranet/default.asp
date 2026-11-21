<%@ Language="VBScript" %>
<!-- #include file="includes/db.inc" -->
<!-- #include file="includes/header.inc" -->
<%
Dim conn, rs
Set conn = OpenBilling()
Set rs = conn.Execute("SELECT TOP 10 InvoiceId, CustomerId, Total FROM dbo.Invoices ORDER BY IssuedOn DESC")
%>
<h1>Latest invoices</h1>
<table>
<% Do Until rs.EOF %>
  <tr><td><a href="invoices.asp?id=<%= rs("InvoiceId") %>"><%= rs("InvoiceId") %></a></td><td><%= rs("Total") %></td></tr>
<%   rs.MoveNext
   Loop
   rs.Close
   conn.Close %>
</table>
</body>
</html>
