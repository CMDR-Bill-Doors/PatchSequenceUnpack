using System;
using System.Xml;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using static System.Net.Mime.MediaTypeNames;
using System.Text;
using System.Linq;
using System.Diagnostics.SymbolStore;
using System.Xml.Linq;
using System.Text.RegularExpressions;

namespace PatchSequenceUnpack
{
    internal class Program
    {
        static XmlWriter result;

        static TextWriter exportLog;

        static XmlReaderSettings readerSettings;

        static StringBuilder strings = new StringBuilder();

        static XmlWriterSettings writterSetting = new XmlWriterSettings();

        static string dir = System.IO.Directory.GetCurrentDirectory();

        static string outputDir;

        static string steamModDir;

        static int i;

        static int j;

        static int k;

        static bool makeLoadFolders;

        static List<string> firstFolders = new List<string>();

        static List<XmlNode> patcheAddToConvert = new List<XmlNode>();

        static List<XmlNode> patcheSequenceToConvert = new List<XmlNode>();

        static Dictionary<string, string> namePackageIDpair = new Dictionary<string, string>();

        static void Main(string[] args)
        {
            Console.WriteLine("Input the patch folder's path");
            dir = Console.ReadLine();

            Console.WriteLine("Input the output folder's path");
            outputDir = Path.Combine(Console.ReadLine(), "Output");

            Directory.CreateDirectory(outputDir);
            exportLog = File.AppendText(Path.Combine(outputDir, "log.txt"));
            Log("Output " + outputDir);
            Log("Create pseudo loadfolder file? Y/N");
            makeLoadFolders = Console.ReadLine().ToLower()[0] == 'y';
            if (makeLoadFolders)
            {
                Log("A pesudo loadfolder file will be created");
                Log("Input the steam mods folder's path");
                steamModDir = Console.ReadLine();
                if (steamModDir != "")
                {
                    Log($"===================Reading steam mod folder at {steamModDir}================");
                    Log($"This may take a while");
                    DirectoryInfo steamDirInfo = new DirectoryInfo(steamModDir);
                    foreach (FileInfo file in steamDirInfo.GetFiles("About.xml", SearchOption.AllDirectories))
                    {
                        LookForPackageID(file);
                    }
                }
            }

            readerSettings = new XmlReaderSettings
            {
                IgnoreComments = true,
                IgnoreWhitespace = true,
                CheckCharacters = false
            };

            writterSetting.Indent = true;
            writterSetting.IndentChars = "\t";

            DirectoryInfo dirInfo = new DirectoryInfo(dir);


            Log($"===================Reading patches================");
            foreach (FileInfo file in dirInfo.GetFiles("*.xml", SearchOption.AllDirectories))
            {
                Read(file);
            }

            if (makeLoadFolders)
            {
                GenLoadFolders();
            }
            Log($"===================Finished================");
            Log($"Converted patches stored at {outputDir}");
            Log($"De-Sequencing of {i} files and {j} patches complete, among which {k} patchOperationAdds are converted to def. Press any key to exit.");
            exportLog.Close();
            Console.ReadKey();
        }

        public static void Log(string logMessage)
        {
            Console.WriteLine(logMessage);
            exportLog.WriteLine(logMessage);
        }

        static void LookForPackageID(FileInfo file)
        {
            try
            {
                XmlDocument xmlDoc;
                StringReader input = new StringReader(File.ReadAllText(file.FullName));
                XmlReader xmlReader = XmlReader.Create(input, readerSettings);
                xmlDoc = new XmlDocument();
                xmlDoc.Load(xmlReader);

                if (xmlDoc.DocumentElement.Name != "ModMetaData")
                {
                    return;
                }

                string name = null;
                string packageId = null;

                foreach (XmlNode childNode in xmlDoc.DocumentElement.ChildNodes)
                {
                    if (childNode.NodeType == XmlNodeType.Element)
                    {
                        if (childNode.Name == "name")
                        {
                            name = childNode.InnerText;
                        }
                        if (childNode.Name == "packageId")
                        {
                            packageId = childNode.InnerText;
                        }
                    }
                }

                if (name != null && packageId != null && !namePackageIDpair.ContainsKey(name))
                {
                    namePackageIDpair.Add(name, packageId);
                    Log($"{name} linked to {packageId}");
                }
            }
            catch (Exception ex)
            {
                Log("Exception reading " + file.Name + ": " + ex);
            }
        }

        static void Read(FileInfo file)
        {
            Log("Reading " + file.FullName);
            try
            {
                XmlDocument xmlDoc;
                StringReader input = new StringReader(File.ReadAllText(file.FullName));
                XmlReader xmlReader = XmlReader.Create(input, readerSettings);
                xmlDoc = new XmlDocument();
                xmlDoc.Load(xmlReader);

                patcheAddToConvert.Clear();
                patcheSequenceToConvert.Clear();
                strings.Clear();

                if (!IsPatch(xmlDoc))
                {
                    Log(file.FullName + " is not a patch");
                    return;
                }

                strings.Append(file.FullName);
                strings.Replace(dir, "");
                strings.Remove(0, 1);
                string filepath = strings.ToString();
                string firstFolder = filepath.Split(@"\".ToCharArray())[0];
                if (!firstFolders.Contains(firstFolder))
                {
                    firstFolders.Add(firstFolder);
                }
                strings.Replace(file.Name, "");
                string directirory = strings.ToString();

                UnPackPatch(xmlDoc);

                GenPatch(firstFolder, directirory, filepath);
                GenDef(firstFolder, directirory, filepath);
                i++;
            }
            catch (Exception ex)
            {
                Log("Exception reading " + file.Name + ": " + ex);
            }
        }

        static void GenPatch(string firstFolder, string directirory, string filePath)
        {
            if (patcheSequenceToConvert.Any())
            {
                Directory.CreateDirectory(Path.Combine(outputDir, firstFolder, "Patches", directirory));
                result = XmlWriter.Create(Path.Combine(outputDir, firstFolder, "Patches", filePath), writterSetting);
                Log("Writing to " + Path.Combine(outputDir, firstFolder, "Patches", filePath));
                result.WriteStartElement("Patch");

                foreach (var patch in patcheSequenceToConvert)
                {
                    CopyNode(patch, "Operation").WriteTo(result);
                }

                result.WriteEndElement();
                result.Close();
            }
        }


        static void GenDef(string firstFolder, string directirory, string filePath)
        {
            if (patcheAddToConvert.Any())
            {
                Directory.CreateDirectory(Path.Combine(outputDir, firstFolder, "Defs", directirory));
                XmlWriter def = XmlWriter.Create(Path.Combine(outputDir, firstFolder, "Defs", filePath), writterSetting);
                Log("Generating def at " + Path.Combine(outputDir, firstFolder, "Defs", filePath));
                def.WriteStartElement("Defs");
                foreach (var value in patcheAddToConvert)
                {
                    foreach (XmlNode patch in value.ChildNodes)
                    {
                        CopyNode(patch, patch.Name).WriteTo(def);
                        k++;
                    }
                }
                def.WriteEndElement();
                def.Close();
            }
        }

        static bool IsPatch(XmlDocument xmlDoc)
        {
            return xmlDoc.DocumentElement.Name == "Patch";
        }

        static bool UnPackPatch(XmlDocument xmlDoc)
        {
            XmlElement documentElement = xmlDoc.DocumentElement;
            if (documentElement.Name != "Patch")
            {
                return false;
            }
            foreach (XmlNode childNode in documentElement.ChildNodes)
            {
                if (childNode.NodeType == XmlNodeType.Element)
                {
                    if (childNode.Name != "Operation")
                    {
                        continue;
                    }
                    FindPatchOps(childNode);
                }
            }

            return true;
        }

        static void FindPatchOps(XmlNode childNode)
        {
            foreach (XmlAttribute attr in childNode.Attributes)
            {
                if (attr.InnerText == "PatchOperationSequence")
                {
                    UnPackSequence(childNode);
                    continue;
                }
                else if (attr.InnerText == "PatchOperationFindMod")
                {
                    UnPackFindMod(childNode);
                    continue;
                }
                else if (attr.InnerText == "PatchOperationAdd")
                {
                    TransformAddIfPossible(childNode);
                    continue;
                }
                else
                {
                    patcheSequenceToConvert.Add(childNode);
                }
            }
        }

        static void UnPackFindMod(XmlNode sequence)
        {
            foreach (XmlNode childNode in sequence.ChildNodes)
            {
                if (childNode.NodeType == XmlNodeType.Element)
                {
                    if (childNode.Name == "match")
                    {
                        FindPatchOps(childNode);
                    }
                }
            }
        }

        static void UnPackSequence(XmlNode sequence)
        {
            foreach (XmlNode childNode in sequence.ChildNodes)
            {
                if (childNode.NodeType == XmlNodeType.Element)
                {
                    if (childNode.Name == "operations")
                    {
                        UnPackSequenceInner(childNode);
                    }
                }
            }
        }

        static void UnPackSequenceInner(XmlNode sequence)
        {
            foreach (XmlNode childNode in sequence.ChildNodes)
            {
                patcheSequenceToConvert.Add(childNode);
                j++;
            }
        }

        static XmlNode CopyNode(XmlNode from, string Name)
        {
            XmlNode to = from.OwnerDocument.CreateElement(Name);

            XmlAttribute[] attributes = new XmlAttribute[from.Attributes.Count];
            from.Attributes.CopyTo(attributes, 0);

            foreach (XmlAttribute attribute in attributes)
            {
                to.Attributes.Append(attribute);
            }

            to.InnerXml = from.InnerXml;

            return to;
        }

        static void TransformAddIfPossible(XmlNode sequence)
        {
            //Skip those with mayrequires
            if (sequence.Attributes.Count == 1)
            {
                XmlNode value = null;
                bool isDef = false; ;
                foreach (XmlNode childNode in sequence.ChildNodes)
                {
                    if (childNode.NodeType == XmlNodeType.Element)
                    {
                        if (childNode.Name == "value")
                        {
                            value = childNode;
                        }
                        if (childNode.Name == "xpath" && childNode.InnerText == "Defs")
                        {
                            isDef = true;
                        }
                    }
                }
                if (isDef && value != null)
                {
                    Log("Transforming PatchOperationAdd into Def.");
                    patcheAddToConvert.Add(value);
                    return;
                }
            }
            patcheSequenceToConvert.Add(sequence);
        }

        static void GenLoadFolders()
        {
            Log("===================Writing Loadfolders================");
            Directory.CreateDirectory(outputDir);
            result = XmlWriter.Create(Path.Combine(outputDir, "LoadFolders.xml"), writterSetting);
            result.WriteStartElement("loadFolders");

            result.WriteComment("This file is a pseudo-LoadFolders, auto created by the unpacker.\n You will need to replace ifModActives to corresponding packageID, and replace REPLACEMENT-CTRL-Hs to some other path like 1.4/mods/ to work.");

            result.WriteStartElement("v1.4");

            result.WriteStartElement("li");
            result.WriteString("/");
            result.WriteEndElement();

            foreach (string s in firstFolders)
            {
                result.WriteStartElement("li");
                string str = $"NOTFOUND[{s}]";
                if (namePackageIDpair.Any() && namePackageIDpair.ContainsKey(s))
                {
                    str = namePackageIDpair[s];
                    Log($"PackageID found for {s}, which is {str}.");
                }
                else
                {
                    Log($"PackageID for {s} not found.");
                }


                result.WriteAttributeString("IfModActive", str);
                result.WriteString("REPLACEMENT-CTRL-H/" + s);
                result.WriteEndElement();
            }

            result.WriteEndElement();
            result.Close();
        }
    }
}
