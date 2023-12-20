using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Tool
{
    class Transform
    {
        public int FileID { get; set; }

        public string? Name { get; set; }

        public int GameObjectFileID { get; set; }
        public int FatherFileID { get; set; }

        public List<Transform> Children { get; set; }

        public Transform()
        {
            Children = [];
        }

        public void WriteToFile(StreamWriter file, int deep)
        {
            for (int i = 0; i < deep; i++)
            {
                file.Write("--");
            }
            file.WriteLine(Name);
            foreach (var child in Children)
            {
                child.WriteToFile(file, deep+1);
            }
        }
    }
    class GameObject
    {
        public string? Name { get; set; }
        public int GameObjectFileID { get; set; }
    }

    class UnityScript
    {
        public string? GUID { get; set; }
        public string? FilePath { get; set; }
        public bool UsedByScene { get; set; }
    }

    
    static partial class Program
    {
        private static readonly bool Verbose = true;
        private static void Cout(this string str, params object[] args) 
        { 
            if (Verbose)
            {
                Console.WriteLine(str, args);
            }
        }
        private static void ParseUnityScenes(string projectFolderPath, string outputFolderPath)
        {
            string assetsFolder = Path.Combine(projectFolderPath, "Assets");

            string scenesFolder = Path.Combine(assetsFolder, "Scenes");
            string scriptsFolder = Path.Combine(assetsFolder, "Scripts");

            string[] sceneFiles;
            try 
            {
                sceneFiles = Directory.GetFiles(scenesFolder, "*.unity", SearchOption.AllDirectories);
            }
            catch (DirectoryNotFoundException e)
            {
                $"Error: {e.Message}".Cout();
                return;
            }

            string[] scriptFiles;
            try 
            {
                scriptFiles = Directory.GetFiles(scriptsFolder, "*.cs", SearchOption.AllDirectories);
            }
            catch (DirectoryNotFoundException e)
            {
                $"Error: {e.Message}".Cout();
                return;
            }

            // Create output folder
            try
            {
                Directory.CreateDirectory(outputFolderPath);
            }
            catch (Exception e)
            {
                $"Error: {e.Message}".Cout();
                return;
            }

            // Parse scripts
            List<UnityScript> unityScripts = [];

            for (int i = 0; i < scriptFiles.Length; i++)
            {
                string scriptFile = scriptFiles[i];

                // Search for GUID in .meta file
                string metaFile = scriptFile + ".meta";
                StreamReader reader = File.OpenText(metaFile);

                if (reader == null)
                {
                    Console.WriteLine($"Error: Could not open file: {scriptFile}");
                    return;
                }

                Console.WriteLine($"Parsing script file: {scriptFile}");

                UnityScript unityScript = new();

                string? line;
                while ((line = reader.ReadLine()) != null) 
                {
                    if (line.StartsWith("guid: "))
                    {
                        string guid = line.Split(":")[1].Trim();
                        Console.WriteLine($"Found GUID: {guid}");

                        unityScript.GUID = guid;

                        break;
                    }
                }

                unityScript.FilePath = scriptFile;
                unityScript.UsedByScene = false;

                unityScripts.Add(unityScript);

                reader.Close();
            }


            for (int i = 0; i < sceneFiles.Length; i++)
            {
                string sceneFile = sceneFiles[i];

                string sceneOutputFile = Path.Combine(outputFolderPath, Path.GetFileNameWithoutExtension(sceneFile) + ".unity.dump");

                
                using StreamReader reader = File.OpenText(sceneFile);

                if (reader == null)
                {
                    Console.WriteLine($"Error: Could not open file: {sceneFile}");
                    return;
                }

                Console.WriteLine($"Parsing scene file: {sceneFile}");

                List<GameObject> allGameObjects = [];
                List<Transform> allTransforms = [];

                Transform rootTransform = new()
                {
                    FileID = 0,
                    Name = "*ROOT*"
                };

                allTransforms.Add(rootTransform);

                string? line;
                while ((line = reader.ReadLine()) != null) 
                {
                    // Check if line contains --- !u!
                   
                    if (line.StartsWith("--- !u!1 ")) // GameObject
                    {
                        GameObject gameObject = new();
                        // split line by space
                        string[] lineSplit = line.Split(' ');
                        string gameObjectId = lineSplit[2];
                        int gameObjectIdInt = int.Parse(gameObjectId[1..]);

                        gameObject.GameObjectFileID = gameObjectIdInt;

                        Console.WriteLine($"Found GameObject with id: {gameObjectIdInt}");

                        // Search for m_Name
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (line.StartsWith("--- !u!")) // Next component
                            {
                                Console.WriteLine($"Error: GameObject [{gameObjectIdInt}] does not contain m_Name. Leaving file...");
                                break;
                            }

                            if (line.StartsWith("  m_Name: "))
                            {
                                string gameObjectName = line.Split(":")[1].Trim();
                                Console.WriteLine($"Found GameObject: {gameObjectName}");

                                gameObject.Name = gameObjectName;

                                break;
                            }
                        }
                        allGameObjects.Add(gameObject);
                    }
                    else if (line.StartsWith("--- !u!4 ")) // Transform
                    {
                        Transform transform = new();

                        // split line by space
                        string[] lineSplit = line.Split(' ');

                        int fileId = int.Parse(lineSplit[2][1..]);

                        transform.FileID = fileId;

                        Console.WriteLine($"Found Transform with id: {fileId}");

                        bool foundGameObject = false;
                        bool foundFather = false;
                        
                        // Search for attributes
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (line.StartsWith("--- !u!")) // Next component
                            {
                                Console.WriteLine($"Error: Transform [{fileId}] does not contain m_Children. Leaving file...");
                                break;
                            }

                            if (line.StartsWith("  m_GameObject: {fileID: "))
                            {
                                string gameObjectFileId = line.Split(":")[2].Trim();
                                int gameObjectFileIdInt = int.Parse(gameObjectFileId[..^1]);
                                Console.WriteLine($"Transform belongs to GameObject: {gameObjectFileIdInt}");

                                transform.GameObjectFileID = gameObjectFileIdInt;

                                foundGameObject = true;
                            }
                            else if (line.StartsWith("  m_Father: {fileID: "))
                            {
                                string fatherFileId = line.Split(":")[2].Trim();
                                int fatherFileIdInt = int.Parse(fatherFileId[..^1]);
                                Console.WriteLine($"Found Father: {fatherFileIdInt}");

                                transform.FatherFileID = fatherFileIdInt;

                                foundFather = true;
                            }

                            if (foundGameObject && foundFather)
                            {
                                break;
                            }
                        }

                        allTransforms.Add(transform);
                    }
                    else if (line.StartsWith("--- !u!114")) // MonoBehaviour
                    {

                        // split line by space
                        string[] lineSplit = line.Split(' ');

                        int fileId = int.Parse(lineSplit[2][1..]);

                        Console.WriteLine($"Found MonoBehaviour with id: {fileId}");

                        // Search for attributes
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (line.StartsWith("--- !u!")) // Next component
                            {
                                Console.WriteLine($"Error: MonoBehaviour [{fileId}] does not contain m_Script. Leaving file...");
                                break;
                            }

                            if (line.StartsWith("  m_Script: {fileID: "))
                            {
                                Regex regex = MScriptRegex();
                                Match match = regex.Match(line);

                                if (!match.Success)
                                {
                                    Console.WriteLine($"Error: Could not parse line: {line}");
                                    break;
                                }

                                string scriptGUID = match.Groups[2].Value;

                                Console.WriteLine($"Found Script: {scriptGUID}");
                                // string scriptFileId = line.Split(":")[2].Trim();
                                // int scriptFileIdInt = int.Parse(scriptFileId[..^1]);
                                //Console.WriteLine($"Found Script: {scriptFileIdInt}");

                                UnityScript? unityScript = unityScripts.Find(x => x.GUID == scriptGUID);

                                if (unityScript == null)
                                {
                                    Console.WriteLine($"Error: Could not find script with GUID: {scriptGUID}");
                                    break;
                                }

                                unityScript.UsedByScene = true;

                                break;
                            }
                        }

                    }
                }

                foreach (var tr in allTransforms)
                {
                    if (tr.FileID == 0) // skip root
                    {
                        continue;
                    }

                    tr.Name = allGameObjects.Find(x => x.GameObjectFileID == tr.GameObjectFileID)?.Name;

                    var father = allTransforms.Find(x => x.FileID == tr.FatherFileID);

                    Debug.Assert(father != null);
                    
                    father.Children.Add(tr);
                }

                using StreamWriter sceneOutStream = File.CreateText(sceneOutputFile);
                if (sceneOutStream == null)
                {
                    Console.WriteLine($"Error: Could not open file: {sceneOutputFile}");
                    return;
                }

                // Write to file. Recursive function
                // Do not write root
                foreach (var child in rootTransform.Children)
                {
                    child.WriteToFile(sceneOutStream, 0);
                }

                using StreamWriter scriptsOutStream = File.CreateText(Path.Combine(outputFolderPath, "UnusedScripts.csv"));
                if (scriptsOutStream == null)
                {
                    Console.WriteLine($"Error: Could not open file: {Path.Combine(outputFolderPath, "UnusedScripts.csv")}");
                    return;
                }

                scriptsOutStream.WriteLine("Relative Path,GUID");
                foreach (var script in unityScripts)
                {
                    if (!script.UsedByScene)
                    {
                        Debug.Assert(script.FilePath != null);

                        string relativePath = Path.GetRelativePath(projectFolderPath, script.FilePath);
                        relativePath = relativePath.Replace('\\', '/');
                        scriptsOutStream.WriteLine(relativePath + "," + script.GUID);
                    }
                }
            }
        }

        static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Error: Invalid number of arguments");
                Console.WriteLine("Usage: tool.exe <project folder path> <output folder path>");
                return;
            }

            foreach (var arg in args)
            {
                Console.WriteLine(arg);
            }
            
            string projectFolderPath = args[0];
            string outputFolderPath = args[1];

            ParseUnityScenes(projectFolderPath, outputFolderPath);  
        }

        [GeneratedRegex(@"\{fileID: (\d+), guid: (\w+), type: (\d+)\}")]
        private static partial Regex MScriptRegex();
    }
}

