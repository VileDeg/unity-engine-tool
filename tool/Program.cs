﻿using System.Diagnostics;

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

                string outputFilePath = Path.Combine(outputFolderPath, Path.GetFileNameWithoutExtension(sceneFile) + ".unity.dump");

                
                StreamReader reader = File.OpenText(sceneFile);

                if (reader == null)
                {
                    Console.WriteLine($"Error: Could not open file: {sceneFile}");
                    return;
                }

                Console.WriteLine($"Parsing scene file: {sceneFile}");

                List<GameObject> allGameObjects = [];
                List<Transform> allTransforms = [];

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
                    
                }

                Transform rootTransform = new();
                rootTransform.FileID = 0;
                rootTransform.Name = "*ROOT*";

                allTransforms.Add(rootTransform);

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

                StreamWriter outputFile = File.CreateText(outputFilePath);
                if (outputFile == null)
                {
                    Console.WriteLine($"Error: Could not open file: {outputFilePath}");
                    return;
                }

                // Write to file. Recursive function
                // Do not write root
                foreach (var child in rootTransform.Children)
                {
                    child.WriteToFile(outputFile, 0);
                }

                reader.Close();
                outputFile.Close();
            }
        }
    }
}

