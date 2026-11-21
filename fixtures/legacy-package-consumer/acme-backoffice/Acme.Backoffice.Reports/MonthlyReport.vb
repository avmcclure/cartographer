Imports System.Data

Public Class MonthlyReport
    Public Function Rows(month As Date) As DataTable
        Return New DataTable("Monthly")
    End Function
End Class
