namespace Vakt.Core.Options;

public class ModelOptions
{
    public const string SectionName = "Model";

    public string ModelPath { get; set; } = "../models/cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4";
    public string ModelDownloadUrl { get; set; } = "https://huggingface.co/microsoft/Phi-3-mini-4k-instruct-onnx/resolve/main/cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4";
}
