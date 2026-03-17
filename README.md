# IKEA 3D Model Downloader

A fast, lightweight command-line tool for downloading IKEA product 3D models (`.glb` files) directly from IKEA product page URLs.

## Features

- 🔗 **URL-based** — just paste any IKEA product page URL
- 📦 **Smart filenames** — generated from the product name, color, and product ID
- 🔄 **Multi-strategy extraction** — Rotera API, DOM query, JSON script parsing, and regex fallback
- 🖥️ **Cross-platform** — native AOT binaries for Windows and Linux
- ⚡ **No runtime required** — self-contained, single-file executables
- 🔁 **Batch mode** — process multiple products in one session

## Download

Pre-built binaries are available on the [Releases](../../releases) page:

| Platform | File |
|----------|------|
| Windows (x64) | `ikea-dl.exe` |
| Linux (x64) | `ikea-dl` |

## Usage

### Basic usage

```
ikea-dl <product-url> [output-directory]
```

| Argument | Description |
|----------|-------------|
| `<product-url>` | Full IKEA product page URL (e.g. `https://www.ikea.com/...`) |
| `[output-directory]` | *(optional)* Directory to save the model. Defaults to `~/Downloads` |

### Examples

**Interactive mode** (prompts for URL and save location):
```sh
ikea-dl
```

**Download a single product**:
```sh
ikea-dl "https://www.ikea.com/us/en/p/kallax-shelf-unit-white-00275848/"
```

**Download to a specific folder**:
```sh
ikea-dl "https://www.ikea.com/us/en/p/kallax-shelf-unit-white-00275848/" "/home/user/3d-models"
```

After the first download, the tool will prompt for additional URLs so you can download multiple models in one session. Press **Enter** on an empty prompt or hit **Ctrl+C** to exit.

### Sample output

```
╔══════════════════════════════════════╗
║       IKEA 3D Model Downloader       ║
╚══════════════════════════════════════╝

  Save location (empty = C:\Users\you\Downloads):
  >

  URL : https://www.ikea.com/us/en/p/kallax-shelf-unit-white-00275848/
  Dir : C:\Users\you\Downloads

  Fetching product page… done

  ✔  KALLAX_Shelf_unit_white_00275848.glb
     Size : 2.34 MB
     Path : C:\Users\you\Downloads\KALLAX_Shelf_unit_white_00275848.glb
```

## Building from Source

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- **Linux only (for AOT):** `clang` and `zlib1g-dev`

  ```sh
  sudo apt-get install -y clang zlib1g-dev
  ```

### Run without publishing

```sh
# Interactive mode
dotnet run --project IkeaDownloader.Console

# With a URL
dotnet run --project IkeaDownloader.Console -- "https://www.ikea.com/..."

# With a URL and output directory
dotnet run --project IkeaDownloader.Console -- "https://www.ikea.com/..." "/path/to/output"
```

### Publish AOT binaries

Use the included publish scripts from the repository root:

**Linux / macOS:**
```sh
bash publish.sh
```

**Windows (PowerShell):**
```powershell
.\publish.ps1
```

Binaries will be placed under `publish/win-x64/ikea-dl.exe` and `publish/linux-x64/ikea-dl`.

You can also publish manually for a specific platform:

```sh
dotnet publish IkeaDownloader.Console/IkeaDownloader.Console.csproj \
  -r linux-x64 -c Release -o publish/linux-x64
```

## How It Works

1. The tool fetches the IKEA product page using a browser-like HTTP client (with `User-Agent` spoofing and automatic decompression).
2. It tries several strategies in order to locate the `.glb` model URL:
   - **Rotera API** — queries IKEA's official 3D viewer API directly.
   - **DOM query** — looks for a `<model-viewer src="...">` element in the parsed HTML.
   - **JSON extraction** — searches embedded `<script>` blocks for a GLB URL.
   - **Regex fallback** — scans the raw HTML for any `.glb` URL pattern.
3. Once found, the model is downloaded and saved with a descriptive filename composed of the product name, color, and product ID.

## Project Structure

```
IkeaDownloader/
├── IkeaDownloader.Console/     # CLI entry point
│   └── Program.cs
├── IkeaDownloader.Core/        # Core download logic (reusable library)
│   ├── IkeaModelDownloader.cs
│   └── Models/
│       └── DownloadResult.cs
├── publish.sh                  # AOT publish script (Linux/macOS)
├── publish.ps1                 # AOT publish script (Windows)
└── .github/workflows/
    └── build.yml               # CI/CD pipeline
```

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| [AngleSharp](https://anglesharp.github.io/) | 1.4.0 | HTML DOM parsing |

## License

This project is licensed under the [MIT License](LICENSE.txt).
