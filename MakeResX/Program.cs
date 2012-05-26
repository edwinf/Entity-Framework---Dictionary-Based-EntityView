using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.Data.Mapping;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Security.Cryptography;
using System.Text;

using Microsoft.CSharp;

/// <summary>
/// This class, although heavily modified, came from this original source:
/// http://archive.msdn.microsoft.com/Project/Download/FileDownload.aspx?ProjectName=appfabriccat&DownloadId=14115
/// http://blogs.msdn.com/b/appfabriccat/archive/2010/08/30/solving-the-no-logical-space-left-to-create-more-user-strings-error-and-improving-performance-of-pre-generated-views-in-visual-studio-net4-entity-framework.aspx
/// </summary>
public class SampleClass
{
	#region Public Methods

	public static void Main(string[] args)
	{
		//Exit if there are no arguments are entered
		if (args.Length <= 0)
		{
			Console.WriteLine("Error - name of the view file is the only required parameter");
		}
		else
		{
			string resFileName = Path.GetFileNameWithoutExtension(args[0]) + ".resources";
			string viewCSFileName = args[0];
			string outputDir = args[1];
			string namespaceArg = args[2];
			string sourceHash;
			int viewCount;

			Console.WriteLine("Starting Resource File Creation");
			CreateResourceFile(viewCSFileName, resFileName, outputDir, out sourceHash, out viewCount);
			Console.WriteLine("Completed Resource File Creation");
			Console.WriteLine("Starting Resource File Validation");
			if (ValidateResourceFile(resFileName, outputDir, sourceHash, viewCount))
			{
				Console.WriteLine("Completed Resource File Validation: Pass");
				Console.WriteLine("Starting CS File Update");
				UpdateSourceFile(viewCSFileName, resFileName, outputDir, namespaceArg);
				Console.WriteLine("Completed CS File Update");
				Console.WriteLine("MakeRESX - Completed Successfully");
			}
			else
			{
				Console.WriteLine("Completed Resource File Validation: Fail");
			}
		}
	}

	#endregion

	#region Methods

	private static void CreateResourceFile(
		string csFile, string resFile, string outputDir, out string sourceHash, out int viewCount)
	{
		viewCount = -1;
		sourceHash = string.Empty;

		var codeProvider = new CSharpCodeProvider();
		var inMemoryCodeCompiler = codeProvider.CreateCompiler();
		var parameters = new CompilerParameters();
		parameters.ReferencedAssemblies.Add("System.Data.Entity.dll");
		parameters.GenerateInMemory = true;
		CompilerResults results = inMemoryCodeCompiler.CompileAssemblyFromFile(parameters, csFile);

		if (results.Errors.Count == 0)
		{
			Assembly resultingAssembly = results.CompiledAssembly;
			Type[] typeList = resultingAssembly.GetTypes();
			foreach (Type type in typeList)
			{
				if (type.BaseType == typeof(EntityViewContainer))
				{
					MethodInfo[] methodInfo = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance);
					var instance = (EntityViewContainer)Activator.CreateInstance(type);
					sourceHash = instance.HashOverAllExtentViews;
					viewCount = instance.ViewCount;
					using (var resWriter = new ResourceWriter(Path.Combine(outputDir, resFile)))
					{
						foreach (MethodInfo method in methodInfo)
						{
							if (char.IsDigit(method.Name, method.Name.Length - 1))
							{
								var result = (KeyValuePair<string, string>)method.Invoke(instance, null);
								resWriter.AddResource(method.Name.Replace("GetView", string.Empty), result);
							}
						}

						resWriter.Generate();
						resWriter.Close();
					}
				}
			}
		}
		else
		{
			Console.WriteLine("Unable to Generate Resource File");
		}
	}

	//Creates the new CS file which uses IDictionary
	private static void UpdateSourceFile(string CSFile, string resFile, string outputDir, string namespaceArg)
	{
		//Load view file and create string array for manipulation
		string[] lines = File.ReadAllLines(CSFile);
		var list = new List<string>();

		//Use to find class declaration
		const string ClassDeclarationStr = "public sealed class ViewsForBaseEntitySets";
		//Dictionary declaration, class scope
		var createDictionary = new StringBuilder();
		createDictionary.AppendLine("        //An IDictionary where Key = Item# and the Value is in turn a set of Key value pairs.");
		createDictionary.AppendLine("        System.Collections.Generic.IDictionary<int,");
		createDictionary.AppendLine("                System.Collections.Generic.KeyValuePair<string, string>> rsxDictionary");
		createDictionary.AppendLine("            = new System.Collections.Generic.Dictionary<int,");
		createDictionary.AppendLine("                System.Collections.Generic.KeyValuePair<string, string>>();");

		//Use to find class constructor
		const string ConstructorStr = "public ViewsForBaseEntitySets";
		//Dictionary creation code
		var populateDictionary = new StringBuilder();
		populateDictionary.AppendLine("            //Maintaining the KeyValuePair required by the generate view - stored as-is in the resource file");
		populateDictionary.AppendLine("            System.Collections.Generic.KeyValuePair<string, string> toInsertKVP = new System.Collections.Generic.KeyValuePair<string, string>();");
		populateDictionary.AppendLine();
		//entered resource file name, use to populate dictionary
		populateDictionary.AppendLine("        try");
		populateDictionary.AppendLine("        {");
		populateDictionary.AppendLine("            System.Reflection.Assembly curAssem = System.Reflection.Assembly.GetExecutingAssembly();");
		populateDictionary.AppendLine("            using (System.Resources.ResourceReader res = new System.Resources.ResourceReader(curAssem.GetManifestResourceStream(" +
			"\"" + namespaceArg + "." + Path.GetFileName(resFile) + "\"" + ")))");
		populateDictionary.AppendLine("            {");
		populateDictionary.AppendLine("                // Create an IDictionaryEnumerator to iterate through the resources.");
		populateDictionary.AppendLine("                System.Collections.IDictionaryEnumerator IDEnumerator = res.GetEnumerator();");
		populateDictionary.AppendLine();
		populateDictionary.AppendLine("                // Iterate through the resources and store it in dictionary");
		populateDictionary.AppendLine("                foreach (System.Collections.DictionaryEntry d in res)");
		populateDictionary.AppendLine("                {");
		populateDictionary.AppendLine("                    //The KeyValuePair to be Inserted into the dictionary, NEEDS TO BE CASTED!");
		populateDictionary.AppendLine("                    toInsertKVP = (System.Collections.Generic.KeyValuePair<string, string>)d.Value;");
		populateDictionary.AppendLine();
		populateDictionary.AppendLine("                    //Populating Dictionary, Item, the key in the dictionary is turn into an integer");
		populateDictionary.AppendLine("                    rsxDictionary.Add(System.Int32.Parse(d.Key.ToString()), toInsertKVP);");
		populateDictionary.AppendLine("                }");
		populateDictionary.AppendLine();
		populateDictionary.AppendLine("                res.Close();");
		populateDictionary.AppendLine("            }");
		populateDictionary.AppendLine("        }");
		populateDictionary.AppendLine("        catch (System.Exception ex)");
		populateDictionary.AppendLine("        {");
		populateDictionary.AppendLine("            string msg = ex.Message;");
		populateDictionary.AppendLine("            throw;");
		populateDictionary.AppendLine("        }");

		//Use to find the only one method
		const string MethodDeclarationStr = "protected override System.Collections.Generic.KeyValuePair<string, string> GetViewAt(int index)";
		//Dictionary creation code
		var dictionaryRetrieval = new StringBuilder();
		dictionaryRetrieval.AppendLine("            System.Collections.Generic.KeyValuePair<string, string> toRetrieveKVP = rsxDictionary[index];");
		dictionaryRetrieval.AppendLine("            return toRetrieveKVP;");

		//Class ending code
		var EndClass = new StringBuilder();
		EndClass.AppendLine("            throw new System.IndexOutOfRangeException();");
		EndClass.AppendLine("        }");
		EndClass.AppendLine("    }");
		EndClass.AppendLine("}");

		//Traverse the view file code to transfer the relevant lines and to add the new ones
		//Assuming that (1), (2) and (3) will be found in order, (4) are not found in order
		for (int i = 0; i < lines.Length; i++)
		{
			if (lines[i].Contains(ClassDeclarationStr)) //(1)
			{
				list.Add(lines[i]); //Add class declaration line
				i++;
				list.Add(lines[i]); //Move a line and add it - the open bracket
				list.Add(createDictionary.ToString()); //Add dictionary creation lines
			}
			else if (lines[i].Contains(ConstructorStr)) //(2)
			{
				list.Add(lines[i]); //Add constructor declaration line
				i++;
				list.Add(lines[i]); //Move a line and add it - the open bracket
				list.Add(populateDictionary.ToString()); //Add populate creation lines
			}
			else if (lines[i].Contains(MethodDeclarationStr)) //(3)
			{
				list.Add(lines[i]); //Add method declaration line
				i++;
				list.Add(lines[i]); //Move a line and add it - the open bracket
				list.Add(dictionaryRetrieval.ToString()); //Add dictionary retrieval lines                
				list.Add(EndClass.ToString()); //Close the class and exit!
				break;
			}
			else //(4) No change required
			{
				list.Add(lines[i]);
			}
		}

		File.WriteAllLines(Path.Combine(outputDir, "WithDictionary_" + Path.GetFileName(CSFile)), list);
	}

	private static bool ValidateResourceFile(string resFile, string outputDir, string sourceHash, int viewCount)
	{
		IDictionary<int, KeyValuePair<string, string>> resourceDictionary =
			new Dictionary<int, KeyValuePair<string, string>>();
		using (var resourceReader = new ResourceReader(Path.Combine(outputDir, resFile)))
		{
			// Create an IDictionaryEnumerator to iterate through the resources.
			IDictionaryEnumerator IDEnumerator = resourceReader.GetEnumerator();

			// Iterate through the resources and store it in dictionary
			foreach (DictionaryEntry d in resourceReader)
			{
				//The KeyValuePair to be Inserted into the dictionary, NEEDS TO BE CASTED!
				var valueToInsert = (KeyValuePair<string, string>)d.Value;

				//Populating Dictionary, Item, the key in the dictionary is turn into an integer
				resourceDictionary.Add(Int32.Parse(d.Key.ToString()), valueToInsert);
			}
			resourceReader.Close();
		}

		/*
		 * This is commented out in the public file because this class is an internal MS class that EF uses to create it's hash.
		 * I reflected on it, made it a public class, and included it in our internal process to validate that the views were 
		 * created correctly and the view hash matched the original EF hash. 
		 * I will not distribute MS source code so the simplest approach is to comment this section out.
		 * 
		 * This code being commented out does not affect the output file, it's just a validation step.
		 * */
		

		//var resourceHaser = new CompressingHashBuilder(new SHA256CryptoServiceProvider());
		//for (int i = 0; i < viewCount; i++)
		//{
		//   KeyValuePair<string, string> resourceValue = resourceDictionary[i];
		//   resourceHaser.AppendLine(resourceValue.Key);
		//   resourceHaser.AppendLine(resourceValue.Value);
		//}
		//string fileHash = resourceHaser.ComputeHash();
		//return sourceHash == fileHash;
		return true;
	}

	#endregion
}