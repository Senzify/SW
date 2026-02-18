Option Explicit

Public Const SETTINGS_FILE_NAME As String = "PropertyN.ini"

' Классы документов для матрицы свойств
Public Const CLS_PART As Integer = 1
Public Const CLS_FASTENER As Integer = 2
Public Const CLS_ASSEMBLY As Integer = 3

' Индексы свойств
Public Const PROP_DESIGNATION As Integer = 1
Public Const PROP_NAME As Integer = 2
Public Const PROP_FULLNAME As Integer = 3
Public Const PROP_CONSTRUCTOR As Integer = 4
Public Const PROP_ISFASTENER As Integer = 5
Public Const PROP_LENGTH As Integer = 6
Public Const PROP_WIDTH As Integer = 7
Public Const PROP_THICKNESS As Integer = 8
Public Const PROP_MASS As Integer = 9
Public Const PROP_MATERIAL As Integer = 10
Public Const PROP_OTHERS As Integer = 11 ' только для удаления

Public Type TMacroSettings
    ConstructorName As String
    ShowReport As Boolean
    CloseDocsAfterRun As Boolean
    ShowSettingsOnStart As Boolean

    FastenerPrefix As String
    SkipReadOnly As Boolean

    ClearProperties As Boolean
    EnableBBox As Boolean

    ExceptionsText As String ' каждая строка = одно исключение (lowercase)

    DeleteFlags(1 To 3, 1 To 11) As Boolean
    CreateFlags(1 To 3, 1 To 10) As Boolean
End Type

Public Sub SetDefaults(ByRef s As TMacroSettings)
    Dim c As Integer, p As Integer

    s.ConstructorName = "Неизвестен"
    s.ShowReport = True
    s.CloseDocsAfterRun = True
    s.ShowSettingsOnStart = False

    s.FastenerPrefix = "П"
    s.SkipReadOnly = True

    s.ClearProperties = True
    s.EnableBBox = True

    s.ExceptionsText = ""

    ' По умолчанию удаляем все наши свойства + другие (поведение близко к старому)
    For c = 1 To 3
        For p = 1 To 11
            s.DeleteFlags(c, p) = True
        Next p
    Next c

    ' По умолчанию создаем как в текущей рабочей логике
    ' Деталь
    s.CreateFlags(CLS_PART, PROP_DESIGNATION) = True
    s.CreateFlags(CLS_PART, PROP_NAME) = True
    s.CreateFlags(CLS_PART, PROP_FULLNAME) = True
    s.CreateFlags(CLS_PART, PROP_CONSTRUCTOR) = True
    s.CreateFlags(CLS_PART, PROP_ISFASTENER) = False
    s.CreateFlags(CLS_PART, PROP_LENGTH) = True
    s.CreateFlags(CLS_PART, PROP_WIDTH) = True
    s.CreateFlags(CLS_PART, PROP_THICKNESS) = True
    s.CreateFlags(CLS_PART, PROP_MASS) = True
    s.CreateFlags(CLS_PART, PROP_MATERIAL) = True

    ' Покупная/исключение
    s.CreateFlags(CLS_FASTENER, PROP_DESIGNATION) = True
    s.CreateFlags(CLS_FASTENER, PROP_NAME) = True
    s.CreateFlags(CLS_FASTENER, PROP_FULLNAME) = True
    s.CreateFlags(CLS_FASTENER, PROP_CONSTRUCTOR) = True
    s.CreateFlags(CLS_FASTENER, PROP_ISFASTENER) = True
    s.CreateFlags(CLS_FASTENER, PROP_LENGTH) = False
    s.CreateFlags(CLS_FASTENER, PROP_WIDTH) = False
    s.CreateFlags(CLS_FASTENER, PROP_THICKNESS) = False
    s.CreateFlags(CLS_FASTENER, PROP_MASS) = False
    s.CreateFlags(CLS_FASTENER, PROP_MATERIAL) = False

    ' Сборка
    s.CreateFlags(CLS_ASSEMBLY, PROP_DESIGNATION) = True
    s.CreateFlags(CLS_ASSEMBLY, PROP_NAME) = True
    s.CreateFlags(CLS_ASSEMBLY, PROP_FULLNAME) = True
    s.CreateFlags(CLS_ASSEMBLY, PROP_CONSTRUCTOR) = True
    s.CreateFlags(CLS_ASSEMBLY, PROP_ISFASTENER) = False
    s.CreateFlags(CLS_ASSEMBLY, PROP_LENGTH) = False
    s.CreateFlags(CLS_ASSEMBLY, PROP_WIDTH) = False
    s.CreateFlags(CLS_ASSEMBLY, PROP_THICKNESS) = False
    s.CreateFlags(CLS_ASSEMBLY, PROP_MASS) = True
    s.CreateFlags(CLS_ASSEMBLY, PROP_MATERIAL) = False
End Sub

Public Function GetDeleteFlag(ByRef s As TMacroSettings, ByVal cls As Integer, ByVal prop As Integer) As Boolean
    GetDeleteFlag = s.DeleteFlags(cls, prop)
End Function

Public Sub SetDeleteFlag(ByRef s As TMacroSettings, ByVal cls As Integer, ByVal prop As Integer, ByVal v As Boolean)
    s.DeleteFlags(cls, prop) = v
End Sub

Public Function GetCreateFlag(ByRef s As TMacroSettings, ByVal cls As Integer, ByVal prop As Integer) As Boolean
    GetCreateFlag = s.CreateFlags(cls, prop)
End Function

Public Sub SetCreateFlag(ByRef s As TMacroSettings, ByVal cls As Integer, ByVal prop As Integer, ByVal v As Boolean)
    s.CreateFlags(cls, prop) = v
End Sub

Public Sub LoadSettings(ByVal iniPath As String, ByRef s As TMacroSettings)
    SetDefaults s

    If Not FileExists(iniPath) Then Exit Sub

    Dim lines As Collection
    Set lines = ReadAllLines(iniPath)
    If lines.Count = 0 Then Exit Sub

    If IsLegacyFormat(lines) Then
        LoadLegacy lines, s
        Exit Sub
    End If

    LoadIniSections lines, s
End Sub

Public Sub SaveSettings(ByVal iniPath As String, ByRef s As TMacroSettings)
    Dim msg As String
    If Not SaveSettingsSafe(iniPath, s, msg) Then
        MsgBox "Не удалось сохранить настройки:" & vbCrLf & msg, vbExclamation, "Настройки макроса"
    End If
End Sub

Public Function SaveSettingsSafe(ByVal iniPath As String, ByRef s As TMacroSettings, Optional ByRef errText As String) As Boolean
    On Error GoTo fail

    Dim fso As Object
    Dim ts As Object
    Dim tempPath As String
    Dim parentDir As String
    Dim c As Integer, p As Integer

    Set fso = CreateObject("Scripting.FileSystemObject")

    parentDir = fso.GetParentFolderName(iniPath)
    If parentDir <> "" Then
        If Not fso.FolderExists(parentDir) Then fso.CreateFolder parentDir
    End If

    tempPath = iniPath & ".tmp"
    If fso.FileExists(tempPath) Then fso.DeleteFile tempPath, True

    Set ts = fso.CreateTextFile(tempPath, True)

    ts.WriteLine "[General]"
    ts.WriteLine "ConstructorName=" & s.ConstructorName
    ts.WriteLine "ShowReport=" & BoolToIni(s.ShowReport)
    ts.WriteLine "CloseDocsAfterRun=" & BoolToIni(s.CloseDocsAfterRun)
    ts.WriteLine "ShowSettingsOnStart=" & BoolToIni(s.ShowSettingsOnStart)
    ts.WriteLine ""

    ts.WriteLine "[Rules]"
    ts.WriteLine "FastenerPrefix=" & s.FastenerPrefix
    ts.WriteLine "SkipReadOnly=" & BoolToIni(s.SkipReadOnly)
    ts.WriteLine ""

    ts.WriteLine "[Processing]"
    ts.WriteLine "ClearProperties=" & BoolToIni(s.ClearProperties)
    ts.WriteLine "EnableBBox=" & BoolToIni(s.EnableBBox)
    ts.WriteLine ""

    ts.WriteLine "[DeleteMatrix]"
    For c = 1 To 3
        For p = 1 To 11
            ts.WriteLine "D_" & CStr(c) & "_" & CStr(p) & "=" & BoolToIni(s.DeleteFlags(c, p))
        Next p
    Next c
    ts.WriteLine ""

    ts.WriteLine "[CreateMatrix]"
    For c = 1 To 3
        For p = 1 To 10
            ts.WriteLine "C_" & CStr(c) & "_" & CStr(p) & "=" & BoolToIni(s.CreateFlags(c, p))
        Next p
    Next c
    ts.WriteLine ""

    ts.WriteLine "[Exceptions]"
    WriteExceptionsFromText ts, s.ExceptionsText

    ts.Close

    If fso.FileExists(iniPath) Then fso.DeleteFile iniPath, True
    Name tempPath As iniPath

    SaveSettingsSafe = True
    Exit Function

fail:
    On Error Resume Next
    If Not ts Is Nothing Then ts.Close
    errText = "Path: " & iniPath & vbCrLf & _
              "Error " & CStr(Err.Number) & ": " & Err.Description
    SaveSettingsSafe = False
End Function

Public Function EditSettings(ByRef s As TMacroSettings) As Boolean
    Dim frm As frmSettings
    Set frm = New frmSettings

    frm.InitFromSettings s
    frm.Show vbModal

    If frm.IsAccepted Then
        frm.WriteToSettings s
        EditSettings = True
    Else
        EditSettings = False
    End If

    Unload frm
End Function

Public Sub ApplySettingsToRuntime(ByRef s As TMacroSettings, ByRef outConstructorName As String, ByRef outExceptions As Object)
    outConstructorName = s.ConstructorName

    Set outExceptions = CreateObject("Scripting.Dictionary")

    Dim v As Variant
    For Each v In SplitExceptionsToArray(s.ExceptionsText)
        If Not outExceptions.Exists(CStr(v)) Then outExceptions.Add CStr(v), True
    Next v
End Sub

Public Function GetMacroFolderPath(ByVal app As Object) As String
    On Error Resume Next

    Dim macroPath As String
    macroPath = CStr(app.GetCurrentMacroPathName)

    If macroPath <> "" And InStrRev(macroPath, "\") > 0 Then
        GetMacroFolderPath = Left(macroPath, InStrRev(macroPath, "\"))
    Else
        GetMacroFolderPath = CurDir$ & "\"
    End If
End Function

Public Function GetSettingsIniPath(ByVal app As Object) As String
    GetSettingsIniPath = GetMacroFolderPath(app) & SETTINGS_FILE_NAME
End Function

Public Sub RunSettingsOnly()
    Dim s As TMacroSettings
    Dim app As Object
    Dim iniPath As String
    Dim errText As String

    Set app = Application.SldWorks
    iniPath = GetSettingsIniPath(app)

    LoadSettings iniPath, s
    If EditSettings(s) Then
        If SaveSettingsSafe(iniPath, s, errText) Then
            MsgBox "Настройки сохранены.", vbInformation, "Настройки макроса"
        Else
            MsgBox "Не удалось сохранить настройки:" & vbCrLf & errText, vbExclamation, "Настройки макроса"
        End If
    End If
End Sub

Private Sub LoadIniSections(ByVal lines As Collection, ByRef s As TMacroSettings)
    Dim currentSection As String
    Dim i As Long, line As String

    currentSection = ""

    For i = 1 To lines.Count
        line = Trim(CStr(lines(i)))

        If line = "" Then GoTo ContinueLoop
        If Left$(line, 1) = ";" Or Left$(line, 1) = "#" Then GoTo ContinueLoop

        If Left$(line, 1) = "[" And Right$(line, 1) = "]" Then
            currentSection = LCase(Mid$(line, 2, Len(line) - 2))
            GoTo ContinueLoop
        End If

        ParseKeyValueLine currentSection, line, s

ContinueLoop:
    Next i
End Sub

Private Sub ParseKeyValueLine(ByVal section As String, ByVal line As String, ByRef s As TMacroSettings)
    Dim p As Long
    Dim key As String, value As String

    p = InStr(1, line, "=")
    If p <= 0 Then Exit Sub

    key = Trim(LCase(Left$(line, p - 1)))
    value = Trim(Mid$(line, p + 1))

    Select Case section
        Case "general"
            Select Case key
                Case "constructorname": s.ConstructorName = value
                Case "showreport": s.ShowReport = IniToBool(value, s.ShowReport)
                Case "closedocsafterrun": s.CloseDocsAfterRun = IniToBool(value, s.CloseDocsAfterRun)
                Case "showsettingsonstart": s.ShowSettingsOnStart = IniToBool(value, s.ShowSettingsOnStart)
            End Select

        Case "rules"
            Select Case key
                Case "fastenerprefix": s.FastenerPrefix = value
                Case "skipreadonly": s.SkipReadOnly = IniToBool(value, s.SkipReadOnly)
            End Select

        Case "processing"
            Select Case key
                Case "clearproperties": s.ClearProperties = IniToBool(value, s.ClearProperties)
                Case "enablebbox": s.EnableBBox = IniToBool(value, s.EnableBBox)
            End Select

        Case "deletematrix"
            ParseMatrixFlag key, value, s, True

        Case "creatematrix"
            ParseMatrixFlag key, value, s, False

        Case "exceptions"
            s.ExceptionsText = AddExceptionToText(s.ExceptionsText, value)
    End Select
End Sub

Private Sub ParseMatrixFlag(ByVal key As String, ByVal value As String, ByRef s As TMacroSettings, ByVal isDelete As Boolean)
    Dim arr As Variant
    Dim cls As Integer, prop As Integer

    arr = Split(key, "_")
    If UBound(arr) <> 2 Then Exit Sub

    If Not IsNumeric(arr(1)) Then Exit Sub
    If Not IsNumeric(arr(2)) Then Exit Sub

    cls = CInt(arr(1))
    prop = CInt(arr(2))

    If cls < 1 Or cls > 3 Then Exit Sub

    If isDelete Then
        If prop < 1 Or prop > 11 Then Exit Sub
        s.DeleteFlags(cls, prop) = IniToBool(value, s.DeleteFlags(cls, prop))
    Else
        If prop < 1 Or prop > 10 Then Exit Sub
        s.CreateFlags(cls, prop) = IniToBool(value, s.CreateFlags(cls, prop))
    End If
End Sub

Private Sub LoadLegacy(ByVal lines As Collection, ByRef s As TMacroSettings)
    Dim isFirstLine As Boolean
    Dim i As Long, line As String

    isFirstLine = True

    For i = 1 To lines.Count
        line = Trim(CStr(lines(i)))
        If line <> "" Then
            If isFirstLine Then
                s.ConstructorName = line
                isFirstLine = False
            Else
                s.ExceptionsText = AddExceptionToText(s.ExceptionsText, line)
            End If
        End If
    Next i

    If Trim$(s.ConstructorName) = "" Then s.ConstructorName = "Неизвестен"
End Sub

Private Function IsLegacyFormat(ByVal lines As Collection) As Boolean
    Dim i As Long
    For i = 1 To lines.Count
        If InStr(1, CStr(lines(i)), "[") > 0 And InStr(1, CStr(lines(i)), "]") > 0 Then
            IsLegacyFormat = False
            Exit Function
        End If
    Next i

    IsLegacyFormat = True
End Function

Private Function ReadAllLines(ByVal path As String) As Collection
    Dim col As New Collection
    Dim fso As Object, ts As Object

    Set fso = CreateObject("Scripting.FileSystemObject")
    Set ts = fso.OpenTextFile(path, 1)

    Do While Not ts.AtEndOfStream
        col.Add ts.ReadLine
    Loop

    ts.Close
    Set ReadAllLines = col
End Function

Private Sub WriteExceptionsFromText(ByVal ts As Object, ByVal exceptionsText As String)
    Dim arr As Variant, v As Variant
    Dim idx As Long

    idx = 1
    arr = SplitExceptionsToArray(exceptionsText)

    For Each v In arr
        ts.WriteLine "item" & CStr(idx) & "=" & CStr(v)
        idx = idx + 1
    Next v
End Sub

Public Function AddExceptionToText(ByVal exceptionsText As String, ByVal value As String) As String
    Dim norm As String
    norm = LCase(Trim$(value))
    If norm = "" Then
        AddExceptionToText = exceptionsText
        Exit Function
    End If

    If ExceptionTextHasValue(exceptionsText, norm) Then
        AddExceptionToText = exceptionsText
    ElseIf Trim$(exceptionsText) = "" Then
        AddExceptionToText = norm
    Else
        AddExceptionToText = exceptionsText & vbLf & norm
    End If
End Function

Public Function ExceptionTextHasValue(ByVal exceptionsText As String, ByVal valueLower As String) As Boolean
    Dim arr As Variant, v As Variant
    arr = SplitExceptionsToArray(exceptionsText)

    For Each v In arr
        If LCase(CStr(v)) = valueLower Then
            ExceptionTextHasValue = True
            Exit Function
        End If
    Next v
End Function

Public Function SplitExceptionsToArray(ByVal exceptionsText As String) As Variant
    Dim raw As String
    raw = Replace(exceptionsText, vbCr, vbLf)

    Do While InStr(raw, vbLf & vbLf) > 0
        raw = Replace(raw, vbLf & vbLf, vbLf)
    Loop

    raw = Trim$(raw)

    If raw = "" Then
        SplitExceptionsToArray = Array()
    Else
        SplitExceptionsToArray = Split(raw, vbLf)
    End If
End Function

Private Function BoolToIni(ByVal v As Boolean) As String
    If v Then
        BoolToIni = "1"
    Else
        BoolToIni = "0"
    End If
End Function

Private Function IniToBool(ByVal v As String, ByVal defaultVal As Boolean) As Boolean
    Dim n As String
    n = LCase(Trim$(v))

    Select Case n
        Case "1", "true", "yes", "on": IniToBool = True
        Case "0", "false", "no", "off": IniToBool = False
        Case Else: IniToBool = defaultVal
    End Select
End Function

Private Function FileExists(ByVal path As String) As Boolean
    Dim fso As Object
    Set fso = CreateObject("Scripting.FileSystemObject")
    FileExists = fso.FileExists(path)
End Function
