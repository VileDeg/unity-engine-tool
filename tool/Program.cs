using System.Diagnostics;
using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
        private static readonly bool Verbose = false;

        private static string? assetsFolderPath;
        private static string? projectFolderPath;
        private static string? outputFolderPath;

        [GeneratedRegex(@"\{fileID: (\d+), guid: (\w+), type: (\d+)\}")]
        private static partial Regex MScriptRegex();

        private static void DebugCout(this string str, params object[] args) 
        { 
            if (Verbose) {
                Console.WriteLine(str, args);
            }
        }

        private static void Cerr(this string str, params object[] args) 
        { 
            Console.WriteLine(str, args);
        }

        private static bool ContainsSerializeFieldAttribute(string code)
        {
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(code);

            // Get the root syntax node of the syntax tree
            SyntaxNode root = syntaxTree.GetRoot();

            // Find all field declarations with the [SerializeField] attribute
            var fieldDeclarations = root.DescendantNodes().OfType<FieldDeclarationSyntax>();
            foreach (var fieldDeclaration in fieldDeclarations)
            {
                foreach (var attributeList in fieldDeclaration.AttributeLists)
                {
                    foreach (var attribute in attributeList.Attributes)
                    {
                        var attributeName = attribute.Name.ToString();
                        if (attributeName == "SerializeField")
                        {
                            // Found a field with [SerializeField] attribute
                            var fieldName = fieldDeclaration.Declaration.Variables.First().Identifier.Text;
                            $"Field {fieldName} has [SerializeField] attribute".DebugCout();
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static void ParseGameObject(string? line, StreamReader reader, List<GameObject> allGameObjects)
        {
            GameObject gameObject = new();
            // split line by space
          
            string[] lineSplit = line!.Split(' ');
            string gameObjectId = lineSplit[2];
            int gameObjectIdInt = int.Parse(gameObjectId[1..]);

            gameObject.GameObjectFileID = gameObjectIdInt;

            $"Found GameObject with id: {gameObjectIdInt}".DebugCout();

            // Search for m_Name
            while ((line = reader.ReadLine()) != null)
            {
                if (line.StartsWith("--- !u!")) { // Next component
                    throw new Exception($"GameObject [{gameObjectIdInt}] does not contain m_Name. Leaving file...");
                }

                if (line.StartsWith("  m_Name: ")) {
                    string gameObjectName = line.Split(":")[1].Trim();
                    $"Found GameObject: {gameObjectName}".DebugCout();

                    gameObject.Name = gameObjectName;
                    break;
                }
            }
            allGameObjects.Add(gameObject);
        }

        private static void ParseTransform(string? line, StreamReader reader, List<Transform> allTransforms)
        {
            Transform transform = new();

            // split line by space
            string[] lineSplit = line!.Split(' ');

            int fileId = int.Parse(lineSplit[2][1..]);

            transform.FileID = fileId;

            $"Found Transform with id: {fileId}".DebugCout();

            bool foundGameObject = false;
            bool foundFather = false;
            
            // Search for attributes
            while ((line = reader.ReadLine()) != null)
            {
                if (line.StartsWith("--- !u!")) // Next component
                {
                    $"Error: Transform [{fileId}] does not contain m_Children. Leaving file...".Cerr();
                    break;
                }

                if (line.StartsWith("  m_GameObject: {fileID: "))
                {
                    string gameObjectFileId = line.Split(":")[2].Trim();
                    int gameObjectFileIdInt = int.Parse(gameObjectFileId[..^1]);
                    $"Transform belongs to GameObject: {gameObjectFileIdInt}".DebugCout();

                    transform.GameObjectFileID = gameObjectFileIdInt;

                    foundGameObject = true;
                }
                else if (line.StartsWith("  m_Father: {fileID: "))
                {
                    string fatherFileId = line.Split(":")[2].Trim();
                    int fatherFileIdInt = int.Parse(fatherFileId[..^1]);
                    $"Found Father: {fatherFileIdInt}".DebugCout();

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

        private static void ParseMonoBehaviour(string? line, StreamReader reader, List<UnityScript> unityScripts)
        {
            // split line by space
            string[] lineSplit = line!.Split(' ');

            int fileId = int.Parse(lineSplit[2][1..]);

            $"Found MonoBehaviour with id: {fileId}".DebugCout();

            // Search for attributes
            while ((line = reader.ReadLine()) != null)
            {
                if (line.StartsWith("--- !u!")) // Next component
                {
                    $"Error: MonoBehaviour [{fileId}] does not contain m_Script. Leaving file...".Cerr();
                    break;
                }

                if (line.StartsWith("  m_Script: {fileID: "))
                {
                    Regex regex = MScriptRegex();
                    Match match = regex.Match(line);

                    if (!match.Success)
                    {
                        $"Error: Could not parse line: {line}".Cerr();
                        break;
                    }

                    string scriptGUID = match.Groups[2].Value;

                    $"Found Script: {scriptGUID}".DebugCout();

                    UnityScript? unityScript = unityScripts.Find(x => x.GUID == scriptGUID);

                    if (unityScript == null)
                    {
                        $"Error: Could not find script with GUID: {scriptGUID}".Cerr();
                        break;
                    }

                    unityScript.UsedByScene = true;

                    break;
                }
            }
        }

        private static List<UnityScript> ParseUnityScripts()
        {
            string scriptsFolder = Path.Combine(assetsFolderPath!, "Scripts");

            string[] scriptFiles = Directory.GetFiles(scriptsFolder, "*.cs", SearchOption.AllDirectories);

            List<UnityScript> unityScripts = [];

            foreach (var scriptFile in scriptFiles) {
                UnityScript unityScript = new();

                string contents = File.ReadAllText(scriptFile);

                unityScript.UsedByScene = ContainsSerializeFieldAttribute(contents);

                // Search for GUID in .meta file
                string metaFile = scriptFile + ".meta";
                using StreamReader reader = File.OpenText(metaFile);
                
                $"Parsing script file: {scriptFile}".DebugCout();

                string? line;
                while ((line = reader.ReadLine()) != null)  {
                    if (line.StartsWith("guid: ")) {
                        string guid = line.Split(":")[1].Trim();
                        $"Found GUID: {guid}".DebugCout();

                        unityScript.GUID = guid;

                        break;
                    }
                }

                unityScript.FilePath = scriptFile;

                unityScripts.Add(unityScript);
            }
            return unityScripts;
        }

        private static void ParseUnityScenes(List<UnityScript> unityScripts)
        {
            string scenesFolder = Path.Combine(assetsFolderPath!, "Scenes");
            
            string[] sceneFiles = Directory.GetFiles(scenesFolder, "*.unity", SearchOption.AllDirectories);

            foreach (var sceneFile in sceneFiles) {
                using StreamReader reader = File.OpenText(sceneFile);

                $"Parsing scene file: {sceneFile}".DebugCout();

                List<GameObject> allGameObjects = [];
                List<Transform> allTransforms = [];

                Transform rootTransform = new()
                {
                    FileID = 0,
                    Name = "*ROOT*"
                };

                allTransforms.Add(rootTransform);

                // Parse scene file
                string? line;
                while ((line = reader.ReadLine()) != null) {
                    if (line.StartsWith("--- !u!1 ")) { // GameObject
                        ParseGameObject(line, reader, allGameObjects);
                    }
                    else if (line.StartsWith("--- !u!4 ")) { // Transform
                        ParseTransform(line, reader, allTransforms);
                    }
                    else if (line.StartsWith("--- !u!114")) { // MonoBehaviour
                        ParseMonoBehaviour(line, reader, unityScripts);
                    }
                }

                // Build transform tree
                foreach (var tr in allTransforms) {
                    if (tr.FileID == 0) { // skip root
                        continue;
                    }

                    tr.Name = allGameObjects.Find(x => x.GameObjectFileID == tr.GameObjectFileID)?.Name;

                    var father = allTransforms.Find(x => x.FileID == tr.FatherFileID);

                    Debug.Assert(father != null);
                    
                    father.Children.Add(tr);
                }

                
                string sceneRelativePath = Path.GetRelativePath(scenesFolder, sceneFile);

                string sceneOutputFilePath = Path.ChangeExtension(
                    Path.Combine(outputFolderPath!, sceneRelativePath), ".unity.dump"
                );

                string? directoryPath = Path.GetDirectoryName(sceneOutputFilePath);
                if (directoryPath != null) {
                    Directory.CreateDirectory(directoryPath);
                }

                using StreamWriter sceneOutStream = File.CreateText(sceneOutputFilePath);

                // Write the hierarchy to file recursively
                // Do not write root
                foreach (var child in rootTransform.Children) {
                    child.WriteToFile(sceneOutStream, 0);
                }
            }

            string unsusedScriptsFilePath = Path.Combine(outputFolderPath!, "UnusedScripts.csv");
            // Write unused scripts to file
            using StreamWriter scriptsOutStream = File.CreateText(unsusedScriptsFilePath);

            scriptsOutStream.WriteLine("Relative Path,GUID");
            foreach (var script in unityScripts) {
                if (!script.UsedByScene) {
                    Debug.Assert(script.FilePath != null);

                    string relativePath = Path.GetRelativePath(projectFolderPath!, script.FilePath);
                    relativePath = relativePath.Replace('\\', '/');

                    scriptsOutStream.WriteLine(relativePath + "," + script.GUID);
                }
            }
        }
        private static void ParseUnityProject()
        {
            Directory.CreateDirectory(outputFolderPath!);

            Stopwatch stopwatch = Stopwatch.StartNew();

            List<UnityScript> unityScripts;
            // Parse scripts
            try {
                unityScripts = ParseUnityScripts();
            } catch (Exception e) {
                throw new Exception($"Could not parse scripts: {e}");
            }

            stopwatch.Stop();
            $"***Parsed scripts in {stopwatch.ElapsedMilliseconds} ms".DebugCout();


            stopwatch.Restart();
            // Parse scenes
            try {
                ParseUnityScenes(unityScripts);
            } catch (Exception e) {
                throw new Exception($"Could not parse scenes: {e}");
            }

            stopwatch.Stop();
            $"***Parsed scenes in {stopwatch.ElapsedMilliseconds} ms".DebugCout();
        }

        static void Main(string[] args)
        {
            if (args.Length != 2) {
                "Error: Invalid number of arguments".Cerr();
                "Usage: tool.exe <project folder path> <output folder path>".Cerr();
                return;
            }

            "Arguments:".DebugCout();
            foreach (var arg in args) {
                arg.DebugCout();
            }
            
            projectFolderPath = args[0];
            outputFolderPath = args[1];

            if (!Directory.Exists(projectFolderPath)) {
                $"Error: Project folder does not exist: {projectFolderPath}".Cerr();
                return;
            }

            assetsFolderPath = Path.Combine(projectFolderPath, "Assets");

            if (!Directory.Exists(assetsFolderPath)) {
                $"Error: Assets folder does not exist: {assetsFolderPath}".Cerr();
                return;
            }

            try {
                ParseUnityProject();  
            } catch (Exception e) {
                $"Error: Could not parse Unity project: {e}".Cerr();
                return;
            }
        }
    }
}

