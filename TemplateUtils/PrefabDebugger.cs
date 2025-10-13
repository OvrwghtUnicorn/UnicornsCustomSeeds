using System.Text;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace UnicornsCustomSeeds.TemplateUtils
{

    public static class PrefabDebugger
    {

        private static string folderName = "CustomSeeds";

        public static void LogPrefabStructure(GameObject prefab, string fileName)
        {
            if (prefab == null)
            {
                Utility.Log("Prefab is null.");
                return;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"# Prefab Structure: {prefab.name}\n");

            Traverse(prefab.transform, sb, 0);

            Utility.Log(sb.ToString());

            string filePath = Path.Combine(Application.persistentDataPath, fileName + ".md");

            try
            {
                string directoryName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string text = Path.Combine(directoryName, folderName);
                bool flag = !Directory.Exists(text);
                if (flag)
                {
                    Directory.CreateDirectory(text);
                }
                string path = Path.Combine(text, fileName + ".md");
                File.WriteAllText(path, sb.ToString());
                Utility.Log($"Structure written to: {filePath}");
            }
            catch (IOException ex)
            {
                Utility.Log($"Failed to write file: {ex.Message}");
            }
        }

        private static void Traverse(Transform current, StringBuilder sb, int depth)
        {
            string indent = new string(' ', depth * 2);
            sb.AppendLine($"{indent}- **{current.gameObject.name}**");

            var components = current.GetComponents<Component>();
            foreach (var comp in components)
            {
                if (comp != null)
                {
                    string compName;
                    try
                    {
                        // Try to get the actual Il2Cpp class name
                        compName = comp.GetIl2CppType().Name;
                    }
                    catch
                    {
                        // Fallback to normal reflection
                        compName = comp.GetType().Name;
                    }

                    sb.AppendLine($"{indent}  - `{compName}`");
                }
                else
                {
                    sb.AppendLine($"{indent}  - `Missing Component`");
                }
            }

            int childCount = current.childCount;
            for (int i = 0; i < current.childCount; i++)
            {
                Traverse(current.GetChild(i), sb, depth + 1);
            }
        }
    }

}
