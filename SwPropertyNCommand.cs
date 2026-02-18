using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace VasilevTools.Commands
{
    public sealed class SwPropertyNCommand : ISwCommand
    {
        public string Name => "PropertyN";
        public string Description => "Заполнение/очистка свойств и Bounding Box для деталей/сборок";

        private const int swDocPART = (int)swDocumentTypes_e.swDocPART;
        private const int swDocASSEMBLY = (int)swDocumentTypes_e.swDocASSEMBLY;

        public void Execute(ISldWorks swApp)
        {
            if (swApp == null) return;

            var active = swApp.ActiveDoc as IModelDoc2;
            if (active == null)
            {
                swApp.SendMsgToUser2("Нет активного документа.", (int)swMessageBoxIcon_e.swMbStop, (int)swMessageBoxBtn_e.swMbOk);
                return;
            }

            if (active.GetType() == (int)swDocumentTypes_e.swDocDRAWING)
            {
                swApp.SendMsgToUser2("Чертёж не поддерживается.", (int)swMessageBoxIcon_e.swMbStop, (int)swMessageBoxBtn_e.swMbOk);
                return;
            }

            var settingsPath = ResolveSettingsPath(swApp);
            Settings settings;
            try
            {
                settings = Settings.Load(settingsPath);
            }
            catch (Exception ex)
            {
                settingsPath = GetUserSettingsPath();
                settings = new Settings();
                swApp.SendMsgToUser2(
                    "Не удалось прочитать файл настроек. Будет использован путь:\n" + settingsPath + "\n" + ex.Message,
                    (int)swMessageBoxIcon_e.swMbWarning,
                    (int)swMessageBoxBtn_e.swMbOk);
            }

            if (settings.ShowSettingsOnStart)
            {
                using (var form = new SettingsForm(settings))
                {
                    var result = form.ShowDialog();
                    if (result == DialogResult.Cancel)
                    {
                        return;
                    }

                    settings = form.Settings;
                    SaveSettingsWithFallback(swApp, settings, ref settingsPath);
                }
            }

            swApp.SendMsgToUser2(
                "Файл настроек PropertyN:\n" + settingsPath,
                (int)swMessageBoxIcon_e.swMbInformation,
                (int)swMessageBoxBtn_e.swMbOk);

            RunProcessing(swApp, active, settings, settingsPath);
        }

        private static void RunProcessing(ISldWorks swApp, IModelDoc2 startModel, Settings settings, string settingsPath)
        {
            var startTime = DateTime.Now;
            var startPath = (startModel.GetPathName() ?? string.Empty).ToLowerInvariant();

            var processedModels = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            var exceptions = settings.GetExceptionsSet();

            var key = string.IsNullOrWhiteSpace(startPath) ? startModel.GetTitle() : startPath;
            if (!processedModels.ContainsKey(key))
            {
                processedModels.Add(key, false);
            }

            if (startModel.GetType() == swDocASSEMBLY)
            {
                CollectPathsRecursive(startModel as IAssemblyDoc, processedModels);
            }

            int totalParts = 0;
            int processedParts = 0;
            int totalAssemblies = 0;
            int processedAssemblies = 0;
            int skippedFasteners = 0;
            int skippedReadOnly = 0;

            var paths = processedModels.Keys.ToList();
            int extraSteps = 1 + (settings.CloseDocsAfterRun ? 1 : 0) + (settings.ShowReport ? 1 : 0);
            int totalSteps = Math.Max(1, paths.Count * 2 + extraSteps);
            int currentStep = 0;

            using (var progress = new ProgressForm())
            {
                progress.Show();
                progress.UpdateProgress(0, totalSteps, "Подготовка к обработке...");
                Application.DoEvents();

                foreach (var path in paths)
                {
                    currentStep++;
                    if (IsSilentPath(path, startPath, settings.FastenerPrefix, exceptions))
                    {
                        progress.UpdateProgress(currentStep, totalSteps, "Тихая обработка: " + SafeFileName(path));
                        ProcessPath(swApp, startPath, path, forceSilent: true, settings,
                            ref totalParts, ref processedParts, ref totalAssemblies, ref processedAssemblies,
                            ref skippedFasteners, ref skippedReadOnly, exceptions);
                        processedModels[path] = true;
                    }
                    else
                    {
                        progress.UpdateProgress(currentStep, totalSteps, "Пропуск тихого прохода: " + SafeFileName(path));
                    }
                }

                foreach (var path in paths)
                {
                    currentStep++;
                    if (!processedModels[path])
                    {
                        progress.UpdateProgress(currentStep, totalSteps, "Обработка геометрии: " + SafeFileName(path));
                        ProcessPath(swApp, startPath, path, forceSilent: false, settings,
                            ref totalParts, ref processedParts, ref totalAssemblies, ref processedAssemblies,
                            ref skippedFasteners, ref skippedReadOnly, exceptions);
                        processedModels[path] = true;
                    }
                    else
                    {
                        progress.UpdateProgress(currentStep, totalSteps, "Уже обработано: " + SafeFileName(path));
                    }
                }

                if (settings.CloseDocsAfterRun)
                {
                    currentStep++;
                    progress.UpdateProgress(currentStep, totalSteps, "Закрытие документов...");
                    CloseAllDocsExceptStart(swApp, startPath);
                }

                if (settings.ShowReport)
                {
                    currentStep++;
                    progress.UpdateProgress(currentStep, totalSteps, "Формирование отчёта...");

                    var elapsed = DateTime.Now - startTime;
                    var report =
                        "ОБРАБОТКА ЗАВЕРШЕНА\n----------------------------\n" +
                        $"Всего деталей: {totalParts}\n" +
                        $" - Геометрия (BBox): {processedParts}\n" +
                        $" - Покупные/Исключения: {skippedFasteners}\n" +
                        $"Всего сборок: {totalAssemblies}\n" +
                        $"Пропущено (ReadOnly): {skippedReadOnly}\n" +
                        "----------------------------\n" +
                        $"Время: {elapsed:hh\\:mm\\:ss}\n" +
                        $"Файл настроек: {settingsPath}";

                    swApp.SendMsgToUser2(report, (int)swMessageBoxIcon_e.swMbInformation, (int)swMessageBoxBtn_e.swMbOk);
                }

                currentStep++;
                progress.UpdateProgress(currentStep, totalSteps, "Завершено");
                PauseWithDoEvents(700);
            }
        }

        private static void PauseWithDoEvents(int milliseconds)
        {
            if (milliseconds <= 0) return;

            var until = DateTime.UtcNow.AddMilliseconds(milliseconds);
            while (DateTime.UtcNow < until)
            {
                Application.DoEvents();
                System.Threading.Thread.Sleep(25);
            }
        }

        private static string SafeFileName(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            var f = Path.GetFileName(path);
            return string.IsNullOrWhiteSpace(f) ? path : f;
        }

        private static bool IsExceptionName(string normalizedNameNoExt, HashSet<string> exceptions)
        {
            if (exceptions == null || exceptions.Count == 0 || string.IsNullOrWhiteSpace(normalizedNameNoExt))
            {
                return false;
            }

            if (exceptions.Contains(normalizedNameNoExt)) return true;

            foreach (var ex in exceptions)
            {
                if (string.IsNullOrWhiteSpace(ex)) continue;

                if (normalizedNameNoExt.StartsWith(ex + "_", StringComparison.Ordinal) ||
                    normalizedNameNoExt.StartsWith(ex + " ", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeRuleKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            var v = value.Trim().ToLowerInvariant().Replace('ё', 'е');
            while (v.Contains("  ")) v = v.Replace("  ", " ");
            return v;
        }

        private static bool IsSilentPath(string docPath, string startDocPath, string fastenerPrefix, HashSet<string> exceptions)
        {
            var lowPath = (docPath ?? string.Empty).ToLowerInvariant();
            if (lowPath == startDocPath) return false;
            if (lowPath.EndsWith(".sldasm", StringComparison.OrdinalIgnoreCase)) return true;

            var fileName = Path.GetFileName(docPath);
            var nameNoExt = Path.GetFileNameWithoutExtension(fileName) ?? string.Empty;
            var ruleName = NormalizeRuleKey(nameNoExt);

            bool startsWithPrefix = !string.IsNullOrWhiteSpace(fastenerPrefix)
                                    && fileName.StartsWith(fastenerPrefix, StringComparison.OrdinalIgnoreCase);
            return startsWithPrefix || IsExceptionName(ruleName, exceptions);
        }

        private static void ProcessPath(
            ISldWorks swApp,
            string startDocPath,
            string docPath,
            bool forceSilent,
            Settings settings,
            ref int totalParts,
            ref int processedParts,
            ref int totalAssemblies,
            ref int processedAssemblies,
            ref int skippedFasteners,
            ref int skippedReadOnly,
            HashSet<string> exceptions)
        {
            if (string.IsNullOrWhiteSpace(docPath)) return;

            var lowPath = docPath.ToLowerInvariant();
            bool isPart = lowPath.EndsWith(".sldprt", StringComparison.OrdinalIgnoreCase);
            bool isAssembly = lowPath.EndsWith(".sldasm", StringComparison.OrdinalIgnoreCase);
            if (!isPart && !isAssembly) return;

            if (settings.SkipReadOnly && File.Exists(docPath))
            {
                var attr = File.GetAttributes(docPath);
                if ((attr & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    skippedReadOnly++;
                    return;
                }
            }

            IModelDoc2 model = null;
            int errors = 0;
            int warnings = 0;

            if (string.Equals(lowPath, startDocPath, StringComparison.OrdinalIgnoreCase))
            {
                model = swApp.ActiveDoc as IModelDoc2;
            }
            else
            {
                var type = isPart ? swDocPART : swDocASSEMBLY;
                int opts = forceSilent ? (int)swOpenDocOptions_e.swOpenDocOptions_Silent : 0;
                model = swApp.OpenDoc6(docPath, type, opts, string.Empty, ref errors, ref warnings) as IModelDoc2;
                if (!forceSilent && model != null)
                {
                    int err = 0;
                    swApp.ActivateDoc3(model.GetTitle(), false, (int)swRebuildOnActivation_e.swRebuildActiveDoc, ref err);
                    model = swApp.ActiveDoc as IModelDoc2;
                }
            }

            if (model == null) return;

            var fileName = Path.GetFileName(docPath);
            var nameNoExt = Path.GetFileNameWithoutExtension(fileName) ?? string.Empty;
            var ruleName = NormalizeRuleKey(nameNoExt);

            bool isFastener = (!string.IsNullOrWhiteSpace(settings.FastenerPrefix)
                               && fileName.StartsWith(settings.FastenerPrefix, StringComparison.OrdinalIgnoreCase))
                              || IsExceptionName(ruleName, exceptions);

            if (settings.ClearProperties)
            {
                ClearAllPropertiesAndBoundingBox(model);
            }

            var cp = model.Extension.CustomPropertyManager[string.Empty];

            if (isPart)
            {
                totalParts++;
                AddBaseProperties(model, cp, settings.ConstructorName, isFastener);

                if (!isFastener)
                {
                    if (settings.EnableBBox)
                    {
                        SetBoundingBoxProperties(swApp, model, cp);
                    }

                    processedParts++;
                }
                else
                {
                    skippedFasteners++;
                }
            }
            else if (isAssembly)
            {
                totalAssemblies++;
                AddBaseProperties(model, cp, settings.ConstructorName, false);
                AddAssemblyMassProperty(cp);
                processedAssemblies++;
            }

            model.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref errors, ref warnings);

            if (!string.Equals(lowPath, startDocPath, StringComparison.OrdinalIgnoreCase))
            {
                swApp.CloseDoc(model.GetTitle());
            }
        }

        private static void CollectPathsRecursive(IAssemblyDoc assy, IDictionary<string, bool> processed)
        {
            if (assy == null) return;
            var comps = assy.GetComponents(false) as object[];
            if (comps == null) return;

            foreach (var c in comps.OfType<IComponent2>())
            {
                var path = (c.GetPathName() ?? string.Empty).ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(path))
                {
                    var mdl = c.GetModelDoc2() as IModelDoc2;
                    path = (mdl?.GetPathName() ?? string.Empty).ToLowerInvariant();
                }

                if (string.IsNullOrWhiteSpace(path) || processed.ContainsKey(path)) continue;

                processed[path] = false;

                if (path.EndsWith(".sldasm", StringComparison.OrdinalIgnoreCase))
                {
                    var child = c.GetModelDoc2() as IAssemblyDoc;
                    CollectPathsRecursive(child, processed);
                }
            }
        }

        private static void ClearAllPropertiesAndBoundingBox(IModelDoc2 model)
        {
            DeleteProps(model.Extension.CustomPropertyManager[string.Empty]);

            var cfgs = model.GetConfigurationNames() as object[];
            if (cfgs != null)
            {
                foreach (var cfg in cfgs.OfType<string>())
                {
                    DeleteProps(model.Extension.CustomPropertyManager[cfg]);
                }
            }

            model.ClearSelection2(true);
            bool selected = model.Extension.SelectByID2("Граничная рамка", "BBOXSKETCH", 0, 0, 0, false, 0, null, 0);
            if (selected)
            {
                model.EditDelete();
            }
        }

        private static void DeleteProps(ICustomPropertyManager cp)
        {
            var names = cp?.GetNames() as object[];
            if (names == null) return;
            foreach (var n in names.OfType<string>())
            {
                cp.Delete2(n);
            }
        }

        private static void AddBaseProperties(IModelDoc2 model, ICustomPropertyManager cp, string constructorName, bool isFastener)
        {
            var fullPath = model.GetPathName();
            var fileName = string.IsNullOrWhiteSpace(fullPath) ? model.GetTitle() : Path.GetFileName(fullPath);
            var cleanName = Path.GetFileNameWithoutExtension(fileName) ?? fileName;

            int idx = cleanName.IndexOf('_');
            if (idx > 0)
            {
                cp.Add3("Обозначение", (int)swCustomInfoType_e.swCustomInfoText, cleanName.Substring(0, idx).Trim(), (int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);
                cp.Add3("Наименование детали", (int)swCustomInfoType_e.swCustomInfoText, cleanName.Substring(idx + 1).Trim(), (int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);
            }
            else
            {
                cp.Add3("Наименование детали", (int)swCustomInfoType_e.swCustomInfoText, cleanName, (int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);
            }

            cp.Add3("Полное наименование", (int)swCustomInfoType_e.swCustomInfoText, cleanName, (int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);
            cp.Add3("Конструктор", (int)swCustomInfoType_e.swCustomInfoText, constructorName, (int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);

            if (isFastener)
            {
                cp.Add3("IsFastener", (int)swCustomInfoType_e.swCustomInfoText, "1", (int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);
            }
        }

        private static void SetBoundingBoxProperties(ISldWorks swApp, IModelDoc2 part, ICustomPropertyManager cp)
        {
            part.EditRebuild3();
            part.ClearSelection2(true);

            bool ok = part.Extension.SelectByID2("Справа", "PLANE", 0, 0, 0, true, 0, null, 0);
            if (!ok)
            {
                ok = part.Extension.SelectByID2("Right Plane", "PLANE", 0, 0, 0, true, 0, null, 0);
            }

            var featMgr = part.FeatureManager;
            var featData = featMgr.CreateDefinition((int)swFeatureNameID_e.swFmBoundingBox) as IBoundingBoxFeatureData;
            if (featData != null)
            {
                featData.ReferenceFaceOrPlane = 0;
                featMgr.CreateFeature(featData);
            }

            cp.Add3("Длина", (int)swCustomInfoType_e.swCustomInfoText, "$PRP:\"Общая длина граничной рамки\"", (int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);
            cp.Add3("Ширина", (int)swCustomInfoType_e.swCustomInfoText, "$PRP:\"Общая ширина граничной рамки\"", (int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);
            cp.Add3("Толщина", (int)swCustomInfoType_e.swCustomInfoText, "$PRP:\"Общая толщина граничной рамки\"", (int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);
            cp.Add3("Масса", (int)swCustomInfoType_e.swCustomInfoText, "\"SW-Mass\"", (int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);
            cp.Add3("Материал", (int)swCustomInfoType_e.swCustomInfoText, "\"SW-Material\"", (int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);

            part.Extension.SelectByID2("Граничная рамка", "BBOXSKETCH", 0, 0, 0, false, 0, null, 0);
            swApp.RunCommand(954, string.Empty); // 954 = Hide/Show sketch items
            part.ClearSelection2(true);
        }

        private static void AddAssemblyMassProperty(ICustomPropertyManager cp)
        {
            cp.Add3("Масса", (int)swCustomInfoType_e.swCustomInfoText, "\"SW-Mass\"", (int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);
        }

        private static void CloseAllDocsExceptStart(ISldWorks swApp, string startDocPath)
        {
            var toClose = new List<string>();
            var doc = swApp.GetFirstDocument() as IModelDoc2;

            while (doc != null)
            {
                var path = (doc.GetPathName() ?? string.Empty).ToLowerInvariant();
                if (!string.Equals(path, startDocPath, StringComparison.OrdinalIgnoreCase))
                {
                    toClose.Add(doc.GetTitle());
                }

                doc = doc.GetNext() as IModelDoc2;
            }

            foreach (var title in toClose)
            {
                swApp.CloseDoc(title);
            }
        }

        private static void SaveSettingsWithFallback(ISldWorks swApp, Settings settings, ref string settingsPath)
        {
            try
            {
                settings.Save(settingsPath);
                return;
            }
            catch (UnauthorizedAccessException)
            {
                // Пробуем следующий доступный путь (например, рядом с кодом/DLL или CurrentDirectory)
            }

            var fallbackPath = GetFirstWritableSettingsPath(swApp);
            if (!string.Equals(fallbackPath, settingsPath, StringComparison.OrdinalIgnoreCase))
            {
                settingsPath = fallbackPath;
                settings.Save(settingsPath);
                swApp.SendMsgToUser2(
                    "Нет прав записи в текущую папку. Настройки сохранены в:\n" + settingsPath,
                    (int)swMessageBoxIcon_e.swMbWarning,
                    (int)swMessageBoxBtn_e.swMbOk);
                return;
            }

            settingsPath = GetUserSettingsPath();
            settings.Save(settingsPath);
            swApp.SendMsgToUser2(
                "Нет прав записи в папку кода/макроса. Временный fallback:\n" + settingsPath,
                (int)swMessageBoxIcon_e.swMbWarning,
                (int)swMessageBoxBtn_e.swMbOk);
        }

        private static string ResolveSettingsPath(ISldWorks swApp)
        {
            // 1) Если уже есть файл в одной из "синхронизируемых" папок — используем его
            foreach (var dir in GetSettingsCandidates(swApp))
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;
                var path = Path.Combine(dir, "PropertyN.ini");
                if (File.Exists(path)) return path;
            }

            // 2) Иначе выбираем первую папку с правом записи (приоритет: папка DLL/кода)
            return GetFirstWritableSettingsPath(swApp);
        }

        private static string GetFirstWritableSettingsPath(ISldWorks swApp)
        {
            foreach (var dir in GetSettingsCandidates(swApp))
            {
                if (!string.IsNullOrWhiteSpace(dir) && IsDirectoryWritable(dir))
                {
                    return Path.Combine(dir, "PropertyN.ini");
                }
            }

            return GetUserSettingsPath();
        }

        private static IEnumerable<string> GetSettingsCandidates(ISldWorks swApp)
        {
            // Приоритет для синхронизации между ПК:
            // 1) рядом с кодом/DLL
            // 2) рядом с макросом (если запуск оттуда)
            // 3) текущая рабочая папка процесса
            yield return GetCodeFolder();
            yield return GetMacroFolder(swApp);
            yield return System.Environment.CurrentDirectory;
        }

        private static string GetCodeFolder()
        {
            try
            {
                var location = typeof(SwPropertyNCommand).Assembly.Location;
                if (!string.IsNullOrWhiteSpace(location))
                {
                    return Path.GetDirectoryName(location);
                }
            }
            catch
            {
                // ignored
            }

            return string.Empty;
        }

        private static string GetMacroFolder(ISldWorks swApp)
        {
            var macroPath = swApp.GetCurrentMacroPathName();
            if (!string.IsNullOrWhiteSpace(macroPath))
            {
                return Path.GetDirectoryName(macroPath);
            }

            return string.Empty;
        }

        private static string GetUserSettingsPath()
        {
            var baseDir = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                "VasilevTools");
            Directory.CreateDirectory(baseDir);
            return Path.Combine(baseDir, "PropertyN.ini");
        }

        private static bool IsDirectoryWritable(string dir)
        {
            try
            {
                Directory.CreateDirectory(dir);
                var probe = Path.Combine(dir, ".write_test.tmp");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private sealed class Settings
        {
            public string ConstructorName { get; set; } = "Неизвестен";
            public string FastenerPrefix { get; set; } = "П";
            public bool SkipReadOnly { get; set; } = true;
            public bool ClearProperties { get; set; } = true;
            public bool EnableBBox { get; set; } = true;
            public bool ShowReport { get; set; } = true;
            public bool CloseDocsAfterRun { get; set; } = true;
            public bool ShowSettingsOnStart { get; set; } = true;
            public string ExceptionsText { get; set; } = string.Empty;

            public HashSet<string> GetExceptionsSet()
            {
                return new HashSet<string>(
                    (ExceptionsText ?? string.Empty)
                        .Split(new[] { "\r\n", "\n", ";", "," }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(NormalizeRuleKey)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                );
            }

            public static Settings Load(string path)
            {
                var s = new Settings();
                if (!File.Exists(path)) return s;

                string section = string.Empty;
                var exceptionItems = new List<string>();
                foreach (var raw in File.ReadAllLines(path))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#")) continue;

                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        section = line.Substring(1, line.Length - 2);
                        continue;
                    }

                    int eq = line.IndexOf('=');
                    if (eq < 0) continue;

                    var key = line.Substring(0, eq).Trim();
                    var value = line.Substring(eq + 1).Trim();

                    if (section.Equals("General", StringComparison.OrdinalIgnoreCase))
                    {
                        if (key.Equals("ConstructorName", StringComparison.OrdinalIgnoreCase)) s.ConstructorName = value;
                        if (key.Equals("ShowReport", StringComparison.OrdinalIgnoreCase)) s.ShowReport = ParseBool(value, s.ShowReport);
                        if (key.Equals("CloseDocsAfterRun", StringComparison.OrdinalIgnoreCase)) s.CloseDocsAfterRun = ParseBool(value, s.CloseDocsAfterRun);
                        if (key.Equals("ShowSettingsOnStart", StringComparison.OrdinalIgnoreCase)) s.ShowSettingsOnStart = ParseBool(value, s.ShowSettingsOnStart);
                    }
                    else if (section.Equals("Rules", StringComparison.OrdinalIgnoreCase))
                    {
                        if (key.Equals("FastenerPrefix", StringComparison.OrdinalIgnoreCase)) s.FastenerPrefix = value;
                        if (key.Equals("SkipReadOnly", StringComparison.OrdinalIgnoreCase)) s.SkipReadOnly = ParseBool(value, s.SkipReadOnly);
                        if (key.Equals("ExceptionsText", StringComparison.OrdinalIgnoreCase))
                        {
                            s.ExceptionsText = value.Replace("\\n", Environment.NewLine);
                        }
                    }
                    else if (section.Equals("Processing", StringComparison.OrdinalIgnoreCase))
                    {
                        if (key.Equals("ClearProperties", StringComparison.OrdinalIgnoreCase)) s.ClearProperties = ParseBool(value, s.ClearProperties);
                        if (key.Equals("EnableBBox", StringComparison.OrdinalIgnoreCase)) s.EnableBBox = ParseBool(value, s.EnableBBox);
                    }
                    else if (section.Equals("Exceptions", StringComparison.OrdinalIgnoreCase))
                    {
                        // Legacy VBA format:
                        // [Exceptions]
                        // item1=...
                        // item2=...
                        if (key.StartsWith("item", StringComparison.OrdinalIgnoreCase) || key.Length > 0)
                        {
                            exceptionItems.Add(value);
                        }
                    }
                }

                // Приоритет legacy-формата [Exceptions], если он есть.
                if (exceptionItems.Count > 0)
                {
                    s.ExceptionsText = string.Join(Environment.NewLine, exceptionItems.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()));
                }

                return s;
            }

            public void Save(string path)
            {
                var exceptionItems = (ExceptionsText ?? string.Empty)
                    .Split(new[] { "\r\n", "\n", ";", "," }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();

                var lines = new List<string>
                {
                    "[General]",
                    $"ConstructorName={ConstructorName}",
                    $"ShowReport={ShowReport}",
                    $"CloseDocsAfterRun={CloseDocsAfterRun}",
                    $"ShowSettingsOnStart={ShowSettingsOnStart}",
                    "",
                    "[Rules]",
                    $"FastenerPrefix={FastenerPrefix}",
                    $"SkipReadOnly={SkipReadOnly}",
                    // Оставляем для обратной совместимости со старой C#-версией
                    $"ExceptionsText={(ExceptionsText ?? string.Empty).Replace("\r\n", "\\n").Replace("\n", "\\n")}",
                    "",
                    "[Processing]",
                    $"ClearProperties={ClearProperties}",
                    $"EnableBBox={EnableBBox}",
                    "",
                    "[Exceptions]"
                };

                for (int i = 0; i < exceptionItems.Count; i++)
                {
                    lines.Add($"item{i + 1}={exceptionItems[i]}");
                }

                var parent = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(parent))
                {
                    Directory.CreateDirectory(parent);
                }

                File.WriteAllLines(path, lines);
            }

            private static bool ParseBool(string value, bool fallback)
            {
                if (bool.TryParse(value, out var b)) return b;
                if (value == "1") return true;
                if (value == "0") return false;
                return fallback;
            }
        }

        private sealed class ProgressForm : Form
        {
            private readonly Panel _frameBar;
            private readonly Panel _bar;
            private readonly Label _lblPercent;
            private readonly Label _lblTitle;

            public ProgressForm()
            {
                Text = "Прогресс обработки";
                Width = 540;
                Height = 150;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                StartPosition = FormStartPosition.CenterScreen;

                _lblTitle = new Label
                {
                    Parent = this,
                    Left = 20,
                    Top = 15,
                    Width = 480,
                    Height = 20,
                    Text = string.Empty
                };

                _frameBar = new Panel
                {
                    Parent = this,
                    Left = 20,
                    Top = 45,
                    Width = 480,
                    Height = 24,
                    BorderStyle = BorderStyle.FixedSingle
                };

                _bar = new Panel
                {
                    Parent = _frameBar,
                    Left = 0,
                    Top = 0,
                    Width = 0,
                    Height = _frameBar.ClientSize.Height,
                    BackColor = System.Drawing.Color.DodgerBlue
                };

                _lblPercent = new Label
                {
                    Parent = this,
                    Left = 20,
                    Top = 78,
                    Width = 100,
                    Height = 20,
                    Text = "0%"
                };
            }

            public void UpdateProgress(int currentValue, int totalValue, string statusText = "")
            {
                double percent = 0d;
                if (totalValue > 0)
                {
                    percent = (double)currentValue / totalValue;
                    if (percent < 0d) percent = 0d;
                    if (percent > 1d) percent = 1d;
                }

                _bar.Width = (int)Math.Round(_frameBar.ClientSize.Width * percent);
                _lblPercent.Text = $"{Math.Round(percent * 100d):0}%";

                if (!string.IsNullOrWhiteSpace(statusText))
                {
                    _lblTitle.Text = statusText;
                }

                Application.DoEvents();
            }
        }

        private sealed class SettingsForm : Form
        {
            private readonly TextBox _txtConstructor;
            private readonly TextBox _txtFastenerPrefix;
            private readonly TextBox _txtExceptions;
            private readonly CheckBox _chkSkipReadOnly;
            private readonly CheckBox _chkClearProperties;
            private readonly CheckBox _chkEnableBBox;
            private readonly CheckBox _chkShowReport;
            private readonly CheckBox _chkCloseDocs;
            private readonly CheckBox _chkShowOnStart;

            public Settings Settings { get; private set; }

            public SettingsForm(Settings source)
            {
                Settings = new Settings
                {
                    ConstructorName = source.ConstructorName,
                    FastenerPrefix = source.FastenerPrefix,
                    ExceptionsText = source.ExceptionsText,
                    SkipReadOnly = source.SkipReadOnly,
                    ClearProperties = source.ClearProperties,
                    EnableBBox = source.EnableBBox,
                    ShowReport = source.ShowReport,
                    CloseDocsAfterRun = source.CloseDocsAfterRun,
                    ShowSettingsOnStart = source.ShowSettingsOnStart
                };

                Text = "PropertyN Settings";
                Width = 760;
                Height = 560;
                StartPosition = FormStartPosition.CenterScreen;

                var tabs = new TabControl { Dock = DockStyle.Fill };
                var pgGeneral = new TabPage("Основные");
                var pgRules = new TabPage("Исключения и правила");
                var pgProcessing = new TabPage("Свойства и BBox");

                tabs.TabPages.Add(pgGeneral);
                tabs.TabPages.Add(pgRules);
                tabs.TabPages.Add(pgProcessing);
                Controls.Add(tabs);

                _txtConstructor = AddLabeledText(pgGeneral, "Конструктор:", 20, 20, 680);
                _chkShowReport = AddCheck(pgGeneral, "Показывать отчёт после выполнения", 20, 60);
                _chkCloseDocs = AddCheck(pgGeneral, "Закрывать документы, кроме стартового", 20, 90);
                _chkShowOnStart = AddCheck(pgGeneral, "Показывать форму настроек при запуске", 20, 120);

                _txtFastenerPrefix = AddLabeledText(pgRules, "Префикс покупных:", 20, 20, 200);
                _chkSkipReadOnly = AddCheck(pgRules, "Пропускать ReadOnly файлы", 20, 60);
                AddLabel(pgRules, "Исключения (по одному значению в строке):", 20, 95, 350);
                _txtExceptions = new TextBox
                {
                    Parent = pgRules,
                    Left = 20,
                    Top = 120,
                    Width = 680,
                    Height = 290,
                    Multiline = true,
                    ScrollBars = ScrollBars.Vertical,
                    WordWrap = false,
                    AcceptsReturn = true
                };

                _chkClearProperties = AddCheck(pgProcessing, "Очищать свойства перед записью", 20, 20);
                _chkEnableBBox = AddCheck(pgProcessing, "Строить Bounding Box для обычных деталей", 20, 50);

                var panel = new Panel { Dock = DockStyle.Bottom, Height = 52 };
                Controls.Add(panel);

                var btnApply = new Button { Text = "Применить", Width = 100, Left = Width - 360, Top = 10 };
                var btnCancel = new Button { Text = "Отмена", Width = 100, Left = Width - 250, Top = 10, DialogResult = DialogResult.Cancel };
                var btnOk = new Button { Text = "ОК", Width = 100, Left = Width - 140, Top = 10, DialogResult = DialogResult.OK };

                btnApply.Click += (_, __) => ApplyFromControls();
                btnOk.Click += (_, __) => ApplyFromControls();

                panel.Controls.Add(btnApply);
                panel.Controls.Add(btnCancel);
                panel.Controls.Add(btnOk);

                AcceptButton = btnOk;
                CancelButton = btnCancel;

                ReadToControls();
            }

            private void ReadToControls()
            {
                _txtConstructor.Text = Settings.ConstructorName;
                _txtFastenerPrefix.Text = Settings.FastenerPrefix;
                _txtExceptions.Text = (Settings.ExceptionsText ?? string.Empty).Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
                _chkSkipReadOnly.Checked = Settings.SkipReadOnly;
                _chkClearProperties.Checked = Settings.ClearProperties;
                _chkEnableBBox.Checked = Settings.EnableBBox;
                _chkShowReport.Checked = Settings.ShowReport;
                _chkCloseDocs.Checked = Settings.CloseDocsAfterRun;
                _chkShowOnStart.Checked = Settings.ShowSettingsOnStart;
            }

            private void ApplyFromControls()
            {
                Settings.ConstructorName = (_txtConstructor.Text ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(Settings.ConstructorName))
                {
                    Settings.ConstructorName = "Неизвестен";
                }

                Settings.FastenerPrefix = (_txtFastenerPrefix.Text ?? string.Empty).Trim();
                Settings.ExceptionsText = _txtExceptions.Text ?? string.Empty;
                Settings.SkipReadOnly = _chkSkipReadOnly.Checked;
                Settings.ClearProperties = _chkClearProperties.Checked;
                Settings.EnableBBox = _chkEnableBBox.Checked;
                Settings.ShowReport = _chkShowReport.Checked;
                Settings.CloseDocsAfterRun = _chkCloseDocs.Checked;
                Settings.ShowSettingsOnStart = _chkShowOnStart.Checked;
            }

            private static Label AddLabel(Control parent, string text, int left, int top, int width)
            {
                var lbl = new Label { Parent = parent, Text = text, Left = left, Top = top, Width = width, Height = 20 };
                return lbl;
            }

            private static TextBox AddLabeledText(Control parent, string caption, int left, int top, int width)
            {
                AddLabel(parent, caption, left, top, 250);
                return new TextBox { Parent = parent, Left = left, Top = top + 20, Width = width };
            }

            private static CheckBox AddCheck(Control parent, string caption, int left, int top)
            {
                return new CheckBox { Parent = parent, Left = left, Top = top, Width = 520, Text = caption };
            }
        }
    }
}
