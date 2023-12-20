// See https://aka.ms/new-console-template for more information
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using System.IO;
using YamlDotNet.RepresentationModel;

namespace Tool
{
    class Transform
    {
        public int FileId { get; set; }

        public List<Transform>? Children { get; set; }
    }
    class GameObject
    {
        public string? Name { get; set; }
        public List<Transform>? Components { get; set; }
    }

    class Program
    {
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
            
            string scenesFolder = Path.GetFullPath(Path.Combine(projectFolderPath, "Assets/Scenes"));

            string[] sceneFiles;
            try 
            {
                sceneFiles = Directory.GetFiles(scenesFolder, "*.unity", SearchOption.AllDirectories);
            }
            catch (DirectoryNotFoundException e)
            {
                Console.WriteLine($"Error: {e.Message}");
                return;
            }

            // Create output folder
            try
            {
                Directory.CreateDirectory(outputFolderPath);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: {e.Message}");
                return;
            }
            

            for (int i = 0; i < sceneFiles.Length; i++)
            {
                string sceneFile = sceneFiles[i];

                string outputFilePath = Path.Combine(outputFolderPath, Path.GetFileNameWithoutExtension(sceneFile) + ".scene.dump");

                StreamWriter outputFile = File.CreateText(outputFilePath);
                StreamReader reader = File.OpenText(sceneFile);

                if (reader == null)
                {
                    Console.WriteLine($"Error: Could not open file: {sceneFile}");
                    return;
                }

                if (outputFile == null)
                {
                    Console.WriteLine($"Error: Could not open file: {outputFilePath}");
                    return;
                }

                Console.WriteLine($"Parsing scene file: {sceneFile}");

                List<GameObject> gameObjects = [];                

                string? line;
                while ((line = reader.ReadLine()) != null) 
                {
                    // Check if line contains --- !u!
                   
                    GameObject? gameObject = null;
                    if (line.StartsWith("--- !u!1 ")) // GameObject
                    {
                        gameObject = new();
                        // split line by space
                        string[] lineSplit = line.Split(' ');
                        string gameObjectId = lineSplit[2];
                        int gameObjectIdInt = int.Parse(gameObjectId[1..]);

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
                                string gameObjectName = line[10..];
                                Console.WriteLine($"Found GameObject: {gameObjectName}");

                                gameObject.Name = gameObjectName;

                                //outputFile.WriteLine(gameObjectName);
                                break;
                            }
                        }
                    }
                    else if (line.StartsWith("--- !u!4 ")) // Transform
                    {
                        if (gameObject == null)
                        {
                            Console.WriteLine($"Error: Transform does not belong to a GameObject. Leaving file...");
                            break;
                        }
                        Transform transform = new();
                        // split line by space
                        string[] lineSplit = line.Split(' ');

                        int fileId = int.Parse(lineSplit[2][1..]);

                        Console.WriteLine($"Found Component with id: {fileId}");

                        // Search for m_Children
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (line.StartsWith("--- !u!")) // Next component
                            {
                                Console.WriteLine($"Error: Transform [{fileId}] does not contain m_Children. Leaving file...");
                                break;
                            }

                            if (line.StartsWith("  m_Name: "))
                            {
                                string componentName = line.Substring(10);
                                Console.WriteLine($"Found Component: {componentName}");

                                outputFile.WriteLine(componentName);
                                break;
                            }
                        }
                    }
                    
                }

                reader.Close();
                outputFile.Close();
            }
        }
    }
}

