using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace TrueMogician.RimWorld.Utility.Tasks;

public class ProcessTranslations : Task {
	private const string _LOG_PREFIX = "[ProcessTranslations] ";

	private sealed class Utf8StringWriter : StringWriter {
		public override Encoding Encoding => new UTF8Encoding(false);
	}

	[Required]
	public string SourceFolder { get; set; }

	[Required]
	public string DestinationFolder { get; set; }

	public string Separator { get; set; } = ".";

	public override bool Execute() {
		if (string.IsNullOrWhiteSpace(SourceFolder)) {
			LogError("Source is required.");
			return false;
		}
		if (string.IsNullOrWhiteSpace(DestinationFolder)) {
			LogError("Destination is required.");
			return false;
		}

		var srcDir = new DirectoryInfo(SourceFolder);
		if (!srcDir.Exists) {
			LogError($"Source directory not found: {srcDir.FullName}");
			return false;
		}

		var dstDir = new DirectoryInfo(DestinationFolder);

		var success = true;
		var filesProcessed = 0;
		var filesWritten = 0;

		try {
			dstDir.Create();
			foreach (var langDir in srcDir.EnumerateDirectories()) {
				string langName = langDir.Name;
				if (string.IsNullOrWhiteSpace(langName))
					continue;

				foreach (var xmlFile in langDir.EnumerateFiles("*.xml", SearchOption.AllDirectories)) {
					filesProcessed++;

					string relativePath = xmlFile.FullName.Substring(langDir.FullName.Length + 1);
					string relativeDir = Path.GetDirectoryName(relativePath) ?? string.Empty;
					string outputPath = Path.Combine(dstDir.FullName, langName, "Keyed", relativeDir, xmlFile.Name);

					XDocument inputDoc;
					try {
						inputDoc = XDocument.Load(xmlFile.FullName, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
					}
					catch (Exception ex) {
						LogError($"Failed to parse XML '{xmlFile.FullName}': {ex.Message}");
						goto Error;
					}
					if (inputDoc.Root is null) {
						LogError($"XML has no root element: {xmlFile.FullName}");
						goto Error;
					}
					var root = inputDoc.Root;
					if (!string.Equals(root.Name.LocalName, "LanguageData", StringComparison.Ordinal)) {
						LogError($"Expected root <LanguageData> in '{xmlFile.FullName}', found <{root.Name}>.");
						goto Error;
					}
					if (!TryFlattenLanguageData(root, out var outputRoot, out var flattenError)) {
						LogError($"{flattenError} (file: {xmlFile.FullName})");
						goto Error;
					}

					var outDoc = new XDocument(
						inputDoc.Declaration ?? new XDeclaration("1.0", "utf-8", null),
						outputRoot
					);
					if (WriteDocumentIfChanged(outputPath, outDoc)) {
						filesWritten++;
						LogMessage($"Wrote {langName}/Keyed/{relativePath.Replace('\\', '/')}", MessageImportance.High);
					}
					continue;
				Error:
					success = false;
				}
			}
		}
		catch (Exception ex) {
			Log.LogErrorFromException(ex, true);
			return false;
		}

		if (success)
			LogMessage($"Processed {filesProcessed} XML file(s); wrote {filesWritten} change(s).", MessageImportance.High);
		return success;
	}

	private static bool WriteDocumentIfChanged(string destinationPath, XDocument document) {
		Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
		string newContent = SerializeXml(document);
		if (File.Exists(destinationPath)) {
			string existing = File.ReadAllText(destinationPath, new UTF8Encoding(false));
			if (string.Equals(existing, newContent, StringComparison.Ordinal))
				return false;
		}
		File.WriteAllText(destinationPath, newContent, new UTF8Encoding(false));
		return true;
	}

	private static string SerializeXml(XDocument document) {
		var settings = new XmlWriterSettings {
			Indent = true,
			IndentChars = "\t",
			OmitXmlDeclaration = false,
			NewLineChars = Environment.NewLine,
			NewLineHandling = NewLineHandling.Replace,
			Encoding = new UTF8Encoding(false)
		};
		using var sw = new Utf8StringWriter();
		using (var xw = XmlWriter.Create(sw, settings))
			document.Save(xw);
		return sw.ToString();
	}

	private bool TryFlattenLanguageData(XElement languageDataRoot, out XElement outputRoot, out string error) {
		outputRoot = new XElement(languageDataRoot.Name);
		var seenKeys = new HashSet<string>(StringComparer.Ordinal);
		error = string.Empty;

		foreach (var child in languageDataRoot.Elements())
			FlattenInto(outputRoot, seenKeys, null, child);

		// Detect if we produced any errors via a sentinel attribute on output root.
		var errorAttr = outputRoot.Attribute("__error");
		if (errorAttr is not null) {
			error = errorAttr.Value;
			outputRoot.Attribute("__error")?.Remove();
			return false;
		}

		return true;
	}

	private void FlattenInto(XElement outputRoot, HashSet<string> seenKeys, string? parentKey, XElement node) {
		string currentKey = CombineKey(parentKey, node.Name.LocalName);
		// If it has element children, recurse; otherwise it's a leaf translation value.
		var elementChildren = node.Elements().ToArray();
		if (elementChildren.Length > 0) {
			foreach (var child in elementChildren)
				FlattenInto(outputRoot, seenKeys, currentKey, child);
			return;
		}
		if (!seenKeys.Add(currentKey)) {
			outputRoot.SetAttributeValue("__error", $"Duplicate translation key '{currentKey}'.");
			return;
		}
		var outElement = new XElement(currentKey, node.Value);
		foreach (var attr in node.Attributes())
			outElement.SetAttributeValue(attr.Name, attr.Value);
		outputRoot.Add(outElement);
	}

	private string CombineKey(string? parentKey, string childName) {
		if (string.IsNullOrEmpty(parentKey))
			return childName;
		if (string.IsNullOrEmpty(childName))
			return parentKey ?? string.Empty;
		return parentKey + Separator + childName;
	}

	private void LogError(string message) => Log.LogError(_LOG_PREFIX + message);

	private void LogMessage(string message, MessageImportance importance = MessageImportance.Normal)
		=> Log.LogMessage(importance, _LOG_PREFIX + message);
}