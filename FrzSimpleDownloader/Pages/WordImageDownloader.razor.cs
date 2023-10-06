using FrzSimpleDownloader.Data;
using FrzSimpleDownloader.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Newtonsoft.Json;
using Olive;

namespace FrzSimpleDownloader.Pages;

public partial class WordImageDownloader
{
	bool Running;
	string Content;

	/// <summary>
	///
	/// </summary>
	// https://music.cdn-wordup.com/Clips/#ID#.mp3
	// #ID# = 0001df27-e712-4065-8246-68abc3d3fa02
	// https://music.cdn-wordup.com/Clips/0001df27-e712-4065-8246-68abc3d3fa02.mp3

	public static string ToNameUrlKey(string name)
	{
		return name
			.OrEmpty()
			.Replace("_", "-")
			.Replace(" ", "-")
			.Replace("_", "-")
			.Where(x => x.IsLetterOrDigit() || x == '-')
			.ToString("")
			.KeepReplacing("--", "-")
			.ToLower();
	}

	MudProgressLinear? progress;
	[Inject] ArvanService Arvan { get; set; }

	string Title;
	string Msg;
	// URL of the text file containing video IDs
	//string textFileUrl = "https://vocavers.cdn-havesh.ir/Words-v19.txt";
	string url;

	string[] words;
	int counter = 0;

	string fileCounter = "WordImageCounter.txt";
	private int _section = 1;

	protected override async Task OnInitializedAsync()
	{
		words = await GetWords();
		progress.Max = words.Length;
		PopulateTotals();

		await base.OnInitializedAsync();
	}

	private async Task<string[]> GetWords()
	{
		using HttpClient httpClient = new HttpClient();
		// Download the text file
		var sectionUrl = $"https://vocavers.s3.ir-thr-at1.arvanstorage.ir/Words-v19.txt";
		var textFileContent = await httpClient.GetStringAsync(sectionUrl);

		return textFileContent
			.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
			.Select(x => x.ToLower())
			.ToArray();
	}

	private void PopulateTotals()
	{
		if (File.Exists(fileCounter))
		{
			var count = File.ReadAllText(fileCounter);
			counter = Convert.ToInt32(count);
		}
		else
		{
			counter = 0;
		}
		progress.Value = counter;

		Title =
			$"Total Downlaod :  {counter} of {words.Length} ({Math.Round((double)((counter * 100) / words.Length), 1)})%";
	}


	bool CheckExistBefore;
	private async Task StartDownlaodClick()
	{
		using var httpClient = new HttpClient();
		if (progress != null)
			progress.Max = words.Length;
		Running = true;

		progress.Max = words.Length;
		foreach (var word in words.Skip(counter))
		{

			if (Stop) break;

			// Download and save the quote voice file by Accent
			var gzBytes = new byte[] { };
			try
			{
				if (CheckExistBefore)
				{
					var itemExist = await Arvan.ItemExist(ArvanService.BUCKET_NAME, "song", $"{word}.mp3");
					if (itemExist)
					{
						Console.WriteLine("------------------------ Item Exists in Bucket");
						Msg = "Item Exists in Bucket";
						goto LabelEND;
					}
				}

				var done = false;
				var x = 0;
				url = $"https://vocavers.s3.ir-thr-at1.arvanstorage.ir/opt/{word}/all.gz";

				do
				{
					try
					{
						gzBytes = await httpClient.GetByteArrayAsync(url);
						done = true;
					}
					catch (Exception e)
					{
						if (!await Arvan.ItemExist(ArvanService.BUCKET_NAME, $"opt/{word}", "all.gz"))
						{
							await DownloadWordDef(word.ToLower(), httpClient);
						}
					}
					Console.WriteLine("In LOOOOOOOOP");

				} while (!done && x++ < 10);

				var unGZip = gzBytes.UnGZip()!;
				var path = @$"C:\PROJECTS\Vocavers\Def\{word}.json";
				await File.WriteAllBytesAsync(path, unGZip);
				var reader = new StreamReader(new MemoryStream(unGZip));
				var jsonString = await reader.ReadToEndAsync();
				var all = JsonConvert.DeserializeObject<AllModel>(jsonString);
				var imgC = 0;
				Msg = $"WORD : {word} - ";
				foreach (var img in all.all.Take(10))
				{
					var imgUrl = $"https://word-images.cdn-wordup.com/opt/{img}";
					var imgBytes = await httpClient.GetByteArrayAsync(imgUrl);
					Arvan.UploadByteArrayToS3(imgBytes, ArvanService.BUCKET_NAME, $"opt/{img}");
					Console.WriteLine($"{word} : Image {++imgC} Pushed to S3 !");
					Msg += " Image " + imgC + " Done , ";
					StateHasChanged();
					await Task.Delay(1);
				}
				reader.Dispose();

			}
			catch (Exception e)
			{
				var errFile = $"error-WordDef.txt";
				await File.AppendAllLinesAsync(errFile, new[] { word });

				Console.WriteLine("************ " + e.Message);
				goto LabelEND;
			}


			LabelEND:
			progress.Value = ++counter;
			Title = $"Total Downlaod :  {counter} of {this.words.Length} ({Math.Round((double)((counter * 100) / this.words.Length), 1)})%";
			Msg = word + " Downlaoded";
			StateHasChanged();

			await File.WriteAllTextAsync(fileCounter, counter.ToString());
			await Task.Delay(1);
		}

		Running = false;
		Stop = false;
	}


	bool Stop = false;
	private void StopDownload()
	{
		Stop = true;
	}


	private async Task<bool> DownloadWordDef(string word, HttpClient httpClient)
	{
		var voiceUrl = string.Format(url, word.ToLowerOrEmpty());
		// Download and save the video file
		byte[] voiceBytes;
		try
		{
			voiceBytes = await httpClient.GetByteArrayAsync(voiceUrl);
		}
		catch (Exception e)
		{
			var errFile = $"error-WordDef.txt";
			await File.AppendAllLinesAsync(errFile, new[] { word });

			Console.WriteLine("************ " + e.Message);
			return false;
		}

		var voiceFileName = $"opt/{word.ToLowerOrEmpty()}/all.gz";
		Console.WriteLine($"Downloaded {voiceFileName}");

		//await File.WriteAllBytesAsync(videoFileName, videoBytes);
		Arvan.UploadByteArrayToS3(voiceBytes, ArvanService.BUCKET_NAME, voiceFileName);
		Console.WriteLine($"Uploaded {voiceFileName}");
		return true;
	}

}