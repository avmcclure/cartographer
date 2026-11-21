<%
' Shared database access for the intranet pages.
Const adCmdStoredProc = 4
Const adInteger = 3
Const adParamInput = 1

Function OpenBilling()
    Dim conn
    Set conn = Server.CreateObject("ADODB.Connection")
    conn.Open "Provider=SQLOLEDB;Data Source=ACMESQL01;Initial Catalog=AcmeBilling;User ID=intranet_web;Password=not-a-real-password;"
    Set OpenBilling = conn
End Function
%>
