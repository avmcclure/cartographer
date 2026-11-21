VERSION 1.0 CLASS
BEGIN
  MultiUse = -1  'True
  Persistable = 0  'NotPersistable
  DataBindingBehavior = 0  'vbNone
  DataSourceBehavior  = 0  'vbNone
  MTSTransactionMode  = 0  'NotAnMTSObject
END
Attribute VB_Name = "Invoice"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = True
Attribute VB_PredeclaredId = False
Attribute VB_Exposed = True
Option Explicit

Private mCustomerId As Long
Private mTotal As Currency
Private mPaid As Currency

Public Property Get CustomerId() As Long
    CustomerId = mCustomerId
End Property

Public Property Get TotalDue() As Currency
    Dim math As New InvoiceMath
    TotalDue = math.RoundMoney(mTotal - mPaid)
End Property

Public Sub Load(ByVal InvoiceId As Long)
    Dim conn As ADODB.Connection
    Dim cmd As New ADODB.Command
    Dim rs As ADODB.Recordset

    Set conn = OpenBilling()
    With cmd
        Set .ActiveConnection = conn
        .CommandText = "dbo.usp_GetInvoice"
        .CommandType = adCmdStoredProc
        .Parameters.Append .CreateParameter("@InvoiceId", adInteger, adParamInput, , InvoiceId)
        Set rs = .Execute
    End With
    If Not rs.EOF Then
        mCustomerId = rs("CustomerId")
        mTotal = rs("Total")
        mPaid = rs("Paid")
    End If
    rs.Close
    conn.Close
End Sub
