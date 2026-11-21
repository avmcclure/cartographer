VERSION 1.0 CLASS
BEGIN
  MultiUse = -1  'True
  Persistable = 0  'NotPersistable
  DataBindingBehavior = 0  'vbNone
  DataSourceBehavior  = 0  'vbNone
  MTSTransactionMode  = 0  'NotAnMTSObject
END
Attribute VB_Name = "InvoiceMath"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = False
Attribute VB_Exposed = False
Option Explicit

' Rounds half away from zero, as the finance team expects.
Public Function RoundMoney(ByVal Amount As Currency) As Currency
    RoundMoney = Fix(Amount * 100 + 0.5 * Sgn(Amount)) / 100
End Function
