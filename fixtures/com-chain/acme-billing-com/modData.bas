Attribute VB_Name = "modData"
Option Explicit

Public Function OpenBilling() As ADODB.Connection
    Dim conn As New ADODB.Connection
    conn.Open "Provider=SQLOLEDB;Data Source=ACMESQL01;Initial Catalog=AcmeBilling;Integrated Security=SSPI;"
    Set OpenBilling = conn
End Function
