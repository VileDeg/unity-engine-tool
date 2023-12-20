// See https://aka.ms/new-console-template for more information
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using System.IO;
using YamlDotNet.RepresentationModel;
using System.Dynamic;

namespace Tool
{
    class GameObject
    {
        public string? Name { get; set; }
        public int TransformFileID { get; set; }
        public List<GameObject> Children { get; set; }

        public GameObject()
        {
            Children = [];
        }

        public GameObject? GetGameObjectById(int id)
        {
            if (TransformFileID == id)
            {
                return this;
            }

            foreach (var child in Children)
            {
                var result = child.GetGameObjectById(id);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        public void WriteToFile(StreamWriter file, int deep)
        {
            for (int i = 0; i < deep; i++)
            {
                file.Write("--");
            }
            file.WriteLine("Name");
            foreach (var child in Children)
            {
                child.WriteToFile(file, deep+1);
            }
        }
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

                
                StreamReader reader = File.OpenText(sceneFile);

                if (reader == null)
                {
                    Console.WriteLine($"Error: Could not open file: {sceneFile}");
                    return;
                }

                

                Console.WriteLine($"Parsing scene file: {sceneFile}");

               
                GameObject rootGameObject = new();

                // Load the stream
                var yaml = new YamlStream();
                yaml.Load(reader);

                // Examine the stream
                var mapping = (YamlMappingNode)yaml.Documents[0].RootNode;
                var component = mapping.Children[0];

                // print all the keys
                foreach (var entry in (YamlMappingNode)component.Value)
                {
                    var key = ((YamlScalarNode)entry.Key).Value;

                    var value = ((YamlScalarNode)entry.Value).Value;

                    if (key == "GameObject")
                    {
                        Console.WriteLine($"GameObject: {value}");
                        //var gameObject = new GameObject();
                        // gameObject.TransformFileID = int.Parse(value);
                        // rootGameObject.Children.Add(gameObject);
                    }
                }

                

                StreamWriter outputFile = File.CreateText(outputFilePath);
                if (outputFile == null)
                {
                    Console.WriteLine($"Error: Could not open file: {outputFilePath}");
                    return;
                }

                // Write to file. Recursive function
                rootGameObject.WriteToFile(outputFile, 0);


                reader.Close();
                outputFile.Close();
            }
        }
    }
}

