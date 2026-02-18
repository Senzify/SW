' *******************************************************
' Версия макроса: 5.7 + Settings UI
' SolidWorks VBA Macro
' Автор: Vasilev Nikita
' -------------------------------------------------------
' Логика сохранена:
' 1. Сбор уникальных путей без открытия геометрии.
' 2. Первый проход: "Тихая" обработка (Покупные, Исключения, Сборки).
' 3. Второй проход: Обработка с активацией окна (Наши детали + Bounding Box).
' 4. BBox-алгоритм не менялся, только переключается флагом EnableBBox.
' *******************************************************

Option Explicit

Dim swApp As SldWorks.SldWorks
Dim ConstructorName As String
Dim processedModels As Object
Dim dictExceptions As Object
Dim startTime As Single

' Счетчики
Dim totalParts As Long, processedParts As Long
Dim totalAssemblies As Long, processedAssemblies As Long
Dim skippedFasteners As Long, skippedReadOnly As Long

Dim ProgressForm As frmProgress
Dim startDocPath As String
Dim startDocTitle As String

' Настройки рантайма (из modSettings)
Dim gFastenerPrefix As String
Dim gSkipReadOnly As Boolean
Dim gClearProperties As Boolean
Dim gEnableBBox As Boolean
Dim gShowReport As Boolean
Dim gCloseDocsAfterRun As Boolean
Dim gDeleteFlags(1 To 3, 1 To 11) As Boolean
Dim gCreateFlags(1 To 3, 1 To 10) As Boolean

Const swDocPART = 1
Const swDocASSEMBLY = 2
Const swOpenDocOptions_Silent = 1
Const swCustomInfoText = 30

Sub main()
    On Error Resume Next
    startTime = Timer

    totalParts = 0: processedParts = 0
    totalAssemblies = 0: processedAssemblies = 0
    skippedFasteners = 0: skippedReadOnly = 0

    Set swApp = Application.SldWorks
    Dim swModel As ModelDoc2
    Set swModel = swApp.ActiveDoc

    If swModel Is Nothing Then Exit Sub
    If swModel.GetType = 3 Then Exit Sub

    startDocPath = LCase(swModel.GetPathName)
    startDocTitle = swModel.GetTitle

    ' 1. ПОДГОТОВКА СЛОВАРЕЙ И НАСТРОЕК
    Set processedModels = CreateObject("Scripting.Dictionary")
    Set dictExceptions = CreateObject("Scripting.Dictionary")

    Dim macroFolder As String
    macroFolder = GetMacroFolderPath(swApp)

    Dim settings As TMacroSettings
    LoadSettings macroFolder & SETTINGS_FILE_NAME, settings

    ' По умолчанию окно настроек открываем на первом запуске (когда INI ещё нет)
    Dim iniPath As String
    iniPath = macroFolder & SETTINGS_FILE_NAME

    If Not FileExistsSimple(iniPath) Then
        settings.ShowSettingsOnStart = True
    End If

    If settings.ShowSettingsOnStart Then
        If Not EditSettings(settings) Then Exit Sub
        Dim saveErr As String
        If Not SaveSettingsSafe(iniPath, settings, saveErr) Then
            MsgBox "Не удалось сохранить настройки:" & vbCrLf & saveErr, vbExclamation, "Настройки макроса"
            Exit Sub
        End If
    End If

    ApplyRuntimeFlags settings

    ' 2. СБОР ПУТЕЙ (БЕЗ ОТКРЫТИЯ)
    If startDocPath <> "" Then
        processedModels.Add startDocPath, False
    Else
        processedModels.Add swModel.GetTitle, False
    End If

    If swModel.GetType = swDocASSEMBLY Then
        CollectPathsRecursive swModel
    End If

    Set ProgressForm = New frmProgress
    ProgressForm.Show vbModeless

    Dim vPaths As Variant, i As Long
    vPaths = processedModels.Keys
    Dim totalItems As Long: totalItems = UBound(vPaths) + 1

    ' 3. ПЕРВЫЙ ПРОХОД: ТИХАЯ ОБРАБОТКА (Сборки, Покупные, Исключения)
    For i = 0 To UBound(vPaths)
        Dim path As String: path = vPaths(i)
        If IsPathSilent(path) Then
            ProgressForm.UpdateProgress i + 1, totalItems, "Тихая обработка: " & GetFileName(path)
            ProcessFileByPath path, True
            processedModels(path) = True
        End If
    Next i

    ' 4. ВТОРОЙ ПРОХОД: ОБЫЧНАЯ ОБРАБОТКА (Остальные детали + Bounding Box)
    For i = 0 To UBound(vPaths)
        path = vPaths(i)
        If processedModels(path) = False Then
            ProgressForm.UpdateProgress i + 1, totalItems, "Обработка геометрии: " & GetFileName(path)
            ProcessFileByPath path, False
            processedModels(path) = True
        End If
    Next i

    ProgressForm.UpdateProgress 100, 100, "Завершено!"
    Pause 0.5
    Unload ProgressForm

    If gCloseDocsAfterRun Then CloseAllDocsExceptStart
    If gShowReport Then GenerateReport
End Sub

Private Sub ApplyRuntimeFlags(ByRef settings As TMacroSettings)
    Dim loadedExceptions As Object
    ApplySettingsToRuntime settings, ConstructorName, loadedExceptions
    Set dictExceptions = loadedExceptions

    gFastenerPrefix = settings.FastenerPrefix
    If Trim$(gFastenerPrefix) = "" Then gFastenerPrefix = "П"

    gSkipReadOnly = settings.SkipReadOnly
    gClearProperties = settings.ClearProperties
    gEnableBBox = settings.EnableBBox
    gShowReport = settings.ShowReport
    gCloseDocsAfterRun = settings.CloseDocsAfterRun

    Dim c As Integer, p As Integer
    For c = 1 To 3
        For p = 1 To 11
            gDeleteFlags(c, p) = settings.DeleteFlags(c, p)
        Next p
        For p = 1 To 10
            gCreateFlags(c, p) = settings.CreateFlags(c, p)
        Next p
    Next c
End Sub


Private Function FileExistsSimple(ByVal path As String) As Boolean
    Dim fso As Object
    Set fso = CreateObject("Scripting.FileSystemObject")
    FileExistsSimple = fso.FileExists(path)
End Function

' =======================================================
' Определение: должен ли файл обрабатываться тихо
' =======================================================
Function IsPathSilent(docPath As String) As Boolean
    Dim lowPath As String: lowPath = LCase(docPath)
    If lowPath = startDocPath Then
        IsPathSilent = False
        Exit Function
    End If

    If InStr(lowPath, ".sldasm") > 0 Then
        IsPathSilent = True
        Exit Function
    End If

    Dim fileName As String: fileName = GetFileName(docPath)
    Dim nameNoExt As String: nameNoExt = GetNameWithoutExtension(fileName)

    If IsFastenerByName(fileName, nameNoExt) Or dictExceptions.Exists(LCase(nameNoExt)) Then
        IsPathSilent = True
    Else
        IsPathSilent = False
    End If
End Function

Private Function IsFastenerByName(ByVal fileName As String, ByVal nameNoExt As String) As Boolean
    Dim pfx As String
    pfx = UCase$(Trim$(gFastenerPrefix))

    If pfx = "" Then pfx = "П"

    IsFastenerByName = (Left$(UCase$(fileName), Len(pfx)) = pfx) Or dictExceptions.Exists(LCase$(nameNoExt))
End Function

' =======================================================
' Обработка файла
' =======================================================
Sub ProcessFileByPath(docPath As String, forceSilent As Boolean)
    On Error Resume Next

    Dim lowPath As String: lowPath = LCase(docPath)
    Dim isPart As Boolean: isPart = (InStr(lowPath, ".sldprt") > 0)
    Dim isAssy As Boolean: isAssy = (InStr(lowPath, ".sldasm") > 0)

    Dim swModel As ModelDoc2
    Dim errors As Long, warnings As Long

    ' Проверка Read-Only
    If gSkipReadOnly Then
        Dim fso As Object: Set fso = CreateObject("Scripting.FileSystemObject")
        If fso.FileExists(docPath) Then
            If (fso.GetFile(docPath).Attributes And 1) = 1 Then
                skippedReadOnly = skippedReadOnly + 1
                Exit Sub
            End If
        End If
    End If

    ' ОТКРЫТИЕ
    If lowPath = startDocPath Then
        Set swModel = swApp.ActiveDoc
    ElseIf forceSilent Then
        Dim dtype As Long: dtype = IIf(isPart, swDocPART, swDocASSEMBLY)
        Set swModel = swApp.OpenDoc6(docPath, dtype, swOpenDocOptions_Silent, "", errors, warnings)
    Else
        Set swModel = swApp.OpenDoc6(docPath, swDocPART, 0, "", errors, warnings)
        If Not swModel Is Nothing Then
            swApp.ActivateDoc3 swModel.GetTitle, False, 0, 0
            Set swModel = swApp.ActiveDoc ' Перепривязка к активному окну
        End If
    End If

    If swModel Is Nothing Then Exit Sub

    ' СВОЙСТВА

    Dim cp As CustomPropertyManager
    Set cp = swModel.Extension.CustomPropertyManager("")

    Dim fileName As String: fileName = GetFileName(docPath)
    Dim nameNoExt As String: nameNoExt = GetNameWithoutExtension(fileName)
    Dim isFastenerProp As Boolean: isFastenerProp = IsFastenerByName(fileName, nameNoExt)

    Dim cls As Integer
    If isAssy Then
        cls = CLS_ASSEMBLY
    ElseIf isFastenerProp Then
        cls = CLS_FASTENER
    Else
        cls = CLS_PART
    End If

    If gClearProperties Then ClearAllPropertiesAndBoundingBox swModel, cls

    If isPart Then
        totalParts = totalParts + 1
        AddBaseProperties swModel, cp, ConstructorName, swDocPART, isFastenerProp, cls

        If Not isFastenerProp Then
            If gEnableBBox Then SetBoundingBoxProperties swModel, cp, cls
            processedParts = processedParts + 1
        Else
            skippedFasteners = skippedFasteners + 1
        End If

    ElseIf isAssy Then
        totalAssemblies = totalAssemblies + 1
        AddBaseProperties swModel, cp, ConstructorName, swDocASSEMBLY, False, cls
        AddAssemblyMassProperty cp, cls
        processedAssemblies = processedAssemblies + 1
    End If

    swModel.Save3 1, errors, warnings
    If lowPath <> startDocPath Then swApp.CloseDoc swModel.GetTitle
End Sub

' =======================================================
' ВСПОМОГАТЕЛЬНЫЕ ФУНКЦИИ
' =======================================================

Function GetNameWithoutExtension(ByVal fileName As String) As String
    Dim lowName As String: lowName = LCase(fileName)
    If Right(lowName, 7) = ".sldprt" Then
        GetNameWithoutExtension = Left(fileName, Len(fileName) - 7)
    ElseIf Right(lowName, 7) = ".sldasm" Then
        GetNameWithoutExtension = Left(fileName, Len(fileName) - 7)
    Else
        GetNameWithoutExtension = fileName
    End If
End Function

Sub AddBaseProperties(m As ModelDoc2, cp As CustomPropertyManager, Constr As String, docType As Long, isF As Boolean, cls As Integer)
    On Error Resume Next
    Dim fullPath As String: fullPath = m.GetPathName
    Dim fileName As String
    If fullPath <> "" Then
        fileName = GetFileName(fullPath)
    Else
        fileName = m.GetTitle
    End If

    Dim cleanName As String: cleanName = GetNameWithoutExtension(fileName)
    Dim uPos As Long: uPos = InStr(cleanName, "_")

    If gCreateFlags(cls, PROP_DESIGNATION) Then
        If uPos > 0 Then
            cp.Add3 "Обозначение", swCustomInfoText, Trim(Left(cleanName, uPos - 1)), 2
        Else
            cp.Add3 "Обозначение", swCustomInfoText, cleanName, 2
        End If
    End If

    If gCreateFlags(cls, PROP_NAME) Then
        If uPos > 0 Then
            cp.Add3 "Наименование детали", swCustomInfoText, Trim(Mid(cleanName, uPos + 1)), 2
        Else
            cp.Add3 "Наименование детали", swCustomInfoText, cleanName, 2
        End If
    End If

    If gCreateFlags(cls, PROP_FULLNAME) Then cp.Add3 "Полное наименование", swCustomInfoText, cleanName, 2
    If gCreateFlags(cls, PROP_CONSTRUCTOR) Then cp.Add3 "Конструктор", swCustomInfoText, Constr, 2

    If isF And gCreateFlags(cls, PROP_ISFASTENER) Then cp.Add3 "IsFastener", swCustomInfoText, "1", 2
End Sub

Sub CollectPathsRecursive(ByVal parentAssy As AssemblyDoc)
    On Error Resume Next
    Dim vComps As Variant, i As Long
    vComps = parentAssy.GetComponents(False)
    If IsEmpty(vComps) Then Exit Sub
    For i = 0 To UBound(vComps)
        Dim swComp As Component2: Set swComp = vComps(i)
        Dim path As String: path = LCase(swComp.GetPathName)
        If path = "" Then
            Dim vModel As ModelDoc2: Set vModel = swComp.GetModelDoc2
            path = LCase(vModel.GetPathName)
        End If
        If Not processedModels.Exists(path) And path <> "" Then
            processedModels.Add path, False
            If InStr(path, ".sldasm") > 0 Then
                Dim swChildModel As ModelDoc2: Set swChildModel = swComp.GetModelDoc2
                If Not swChildModel Is Nothing Then CollectPathsRecursive swChildModel
            End If
        End If
    Next i
End Sub

Sub ClearAllPropertiesAndBoundingBox(m As ModelDoc2, cls As Integer)
    On Error Resume Next

    Dim cp As CustomPropertyManager
    Set cp = m.Extension.CustomPropertyManager("")
    DeletePropsByMatrix cp, cls

    Dim vCfg As Variant, i As Long
    vCfg = m.GetConfigurationNames
    If Not IsEmpty(vCfg) Then
        For i = 0 To UBound(vCfg)
            Set cp = m.Extension.CustomPropertyManager(vCfg(i))
            DeletePropsByMatrix cp, cls
        Next i
    End If

    ' Удаляем эскиз рамки, если в матрице удаления отмечены габариты или "Другие"
    If gDeleteFlags(cls, PROP_LENGTH) Or gDeleteFlags(cls, PROP_WIDTH) Or gDeleteFlags(cls, PROP_THICKNESS) Or gDeleteFlags(cls, PROP_OTHERS) Then
        Dim boolstatus As Boolean
        m.ClearSelection2 True
        boolstatus = m.Extension.SelectByID2("Граничная рамка", "BBOXSKETCH", 0, 0, 0, False, 0, Nothing, 0)
        If boolstatus Then m.EditDelete
    End If
End Sub

Private Sub DeletePropsByMatrix(cp As CustomPropertyManager, cls As Integer)
    On Error Resume Next

    Dim props As Variant, i As Long
    props = cp.GetNames
    If IsEmpty(props) Then Exit Sub

    For i = 0 To UBound(props)
        Dim pn As String
        pn = CStr(props(i))
        If ShouldDeleteProperty(cls, pn) Then cp.Delete2 pn
    Next i
End Sub

Private Function ShouldDeleteProperty(cls As Integer, ByVal propName As String) As Boolean
    Dim n As String
    n = LCase(Trim$(propName))

    Select Case n
        Case LCase("Обозначение")
            ShouldDeleteProperty = gDeleteFlags(cls, PROP_DESIGNATION)
        Case LCase("Наименование детали")
            ShouldDeleteProperty = gDeleteFlags(cls, PROP_NAME)
        Case LCase("Полное наименование")
            ShouldDeleteProperty = gDeleteFlags(cls, PROP_FULLNAME)
        Case LCase("Конструктор")
            ShouldDeleteProperty = gDeleteFlags(cls, PROP_CONSTRUCTOR)
        Case LCase("IsFastener")
            ShouldDeleteProperty = gDeleteFlags(cls, PROP_ISFASTENER)
        Case LCase("Длина")
            ShouldDeleteProperty = gDeleteFlags(cls, PROP_LENGTH)
        Case LCase("Ширина")
            ShouldDeleteProperty = gDeleteFlags(cls, PROP_WIDTH)
        Case LCase("Толщина")
            ShouldDeleteProperty = gDeleteFlags(cls, PROP_THICKNESS)
        Case LCase("Масса")
            ShouldDeleteProperty = gDeleteFlags(cls, PROP_MASS)
        Case LCase("Материал")
            ShouldDeleteProperty = gDeleteFlags(cls, PROP_MATERIAL)
        Case Else
            ShouldDeleteProperty = gDeleteFlags(cls, PROP_OTHERS)
    End Select
End Function

Sub SetBoundingBoxProperties(Part As ModelDoc2, cp As CustomPropertyManager, cls As Integer)
    On Error Resume Next

    Dim needDims As Boolean
    needDims = gCreateFlags(cls, PROP_LENGTH) Or gCreateFlags(cls, PROP_WIDTH) Or gCreateFlags(cls, PROP_THICKNESS)

    If needDims Then
        ' Принудительное перестроение перед созданием рамки
        Part.EditRebuild3

        Dim ok As Boolean
        Part.ClearSelection2 True

        ' Попытка выбора плоскости "Справа" (RU/EN)
        ok = Part.Extension.SelectByID2("Справа", "PLANE", 0, 0, 0, True, 0, Nothing, 0)
        If Not ok Then ok = Part.Extension.SelectByID2("Right Plane", "PLANE", 0, 0, 0, True, 0, Nothing, 0)

        ' Создание фичера BBox
        Dim swFeatMgr As FeatureManager: Set swFeatMgr = Part.FeatureManager
        Dim swFeatData As BoundingBoxFeatureData: Set swFeatData = swFeatMgr.CreateDefinition(swFeatureNameID_e.swFmBoundingBox)

        If Not swFeatData Is Nothing Then
            swFeatData.ReferenceFaceOrPlane = 0
            Dim swFeat As Feature: Set swFeat = swFeatMgr.CreateFeature(swFeatData)

            If gCreateFlags(cls, PROP_LENGTH) Then cp.Add3 "Длина", swCustomInfoText, "$PRP:""" & "Общая длина граничной рамки" & """", 2
            If gCreateFlags(cls, PROP_WIDTH) Then cp.Add3 "Ширина", swCustomInfoText, "$PRP:""" & "Общая ширина граничной рамки" & """", 2
            If gCreateFlags(cls, PROP_THICKNESS) Then cp.Add3 "Толщина", swCustomInfoText, "$PRP:""" & "Общая толщина граничной рамки" & """", 2
        End If

        ' Скрытие эскиза рамки
        Part.Extension.SelectByID2 "Граничная рамка", "BBOXSKETCH", 0, 0, 0, False, 0, Nothing, 0
        swApp.RunCommand 954, ""
        Part.ClearSelection2 True
    End If

    If gCreateFlags(cls, PROP_MASS) Then cp.Add3 "Масса", swCustomInfoText, """SW-Mass""", 2
    If gCreateFlags(cls, PROP_MATERIAL) Then cp.Add3 "Материал", swCustomInfoText, """SW-Material""", 2
End Sub

Sub AddAssemblyMassProperty(cp As CustomPropertyManager, cls As Integer)
    On Error Resume Next
    If gCreateFlags(cls, PROP_MASS) Then cp.Add3 "Масса", swCustomInfoText, """SW-Mass""", 2
End Sub

Function GetFileName(path As String) As String
    If InStr(path, "\") > 0 Then GetFileName = Mid(path, InStrRev(path, "\") + 1) Else GetFileName = path
End Function

Sub CloseAllDocsExceptStart()
    On Error Resume Next
    Dim d As ModelDoc2: Set d = swApp.GetFirstDocument
    Dim titles As New Collection
    Do While Not d Is Nothing
        If LCase(d.GetPathName) <> startDocPath Then titles.Add d.GetTitle
        Set d = d.GetNext
    Loop
    Dim i As Long
    For i = 1 To titles.Count: swApp.CloseDoc titles(i): Next i
End Sub

Sub GenerateReport()
    Dim report As String, totalTime As Single: totalTime = Timer - startTime
    report = "ОБРАБОТКА ЗАВЕРШЕНА" & vbCrLf & "----------------------------" & vbCrLf & _
             "Всего деталей: " & totalParts & vbCrLf & _
             " - Геометрия (BBox): " & processedParts & vbCrLf & _
             " - Покупные/Исключения: " & skippedFasteners & vbCrLf & _
             "Всего сборок: " & totalAssemblies & vbCrLf & _
             "Пропущено (ReadOnly): " & skippedReadOnly & vbCrLf & _
             "----------------------------" & vbCrLf & _
             "Время: " & Format(totalTime / 86400, "hh:nn:ss")
    MsgBox report, vbInformation, "Отчёт V5.7"
End Sub

Sub Pause(seconds As Double)
    Dim endTime As Double: endTime = Timer + seconds
    Do While Timer < endTime: DoEvents: Loop
End Sub
