using System.Text.RegularExpressions;
using Newtonsoft.Json;
using NJsonSchema;
using NJsonSchema.References;

namespace NSwag
{
    /// <summary>The Swagger response.</summary>
    public class OpenApiResponse : JsonReferenceBase<OpenApiResponse>, IJsonReference
    {
        private static readonly Regex AppJsonRegex = new Regex(@"application\/(\S+?)?\+?json;?(\S+)?", RegexOptions.Compiled);

        [JsonExtensionData]
        public IDictionary<string, object> ExtensionData { get; set; }

        [JsonIgnore]
        public object Parent { get; internal set; }

        [JsonIgnore]
        public OpenApiResponse ActualResponse => Reference ?? this;

        [JsonProperty(PropertyName = "description", Order = 1)]
        public string Description { get; set; } = "";

        [JsonProperty(PropertyName = "headers", Order = 3, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public OpenApiHeaders Headers { get; } = [];

        [JsonProperty(PropertyName = "x-nullable", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public bool? IsNullableRaw { internal get; set; }

        [JsonProperty(PropertyName = "x-expectedSchemas", Order = 7, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public ICollection<JsonExpectedSchema> ExpectedSchemas { get; set; }

        [JsonProperty(PropertyName = "content", Order = 4, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public IDictionary<string, OpenApiMediaType> Content { get; } = new Dictionary<string, OpenApiMediaType>();

        [JsonProperty(PropertyName = "links", Order = 5, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public IDictionary<string, OpenApiLink> Links { get; } = new Dictionary<string, OpenApiLink>();

        [JsonProperty(PropertyName = "schema", Order = 2, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public JsonSchema Schema
        {
            get
            {
                foreach (var item in Content)
                {
                    if (item.Value?.Schema != null)
                        return item.Value.Schema;
                }
                return null;
            }
            set => UpdateContent(value, Examples);
        }

        [JsonProperty(PropertyName = "examples", Order = 6, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public object Examples
        {
            get
            {
                foreach (var item in Content)
                {
                    if (item.Value?.Example != null)
                        return item.Value.Example;
                }
                return null;
            }
            set => UpdateContent(Schema, value);
        }

        private void UpdateContent(JsonSchema schema, object example)
        {
            Content.Clear();

            if (schema == null && example == null)
                return;

            var mimeType = schema?.IsBinary == true ? "application/octet-stream" : "application/json";
            Content[mimeType] = new OpenApiMediaType
            {
                Schema = schema,
                Example = example
            };
        }

        public bool IsNullable(SchemaType schemaType)
        {
            return IsNullable(schemaType, false);
        }

        public bool IsNullable(SchemaType schemaType, bool fallbackValue)
        {
            if (schemaType == SchemaType.Swagger2)
                return IsNullableRaw ?? fallbackValue;

            return ActualResponse.Schema?.IsNullable(schemaType) ?? false;
        }

        public bool IsBinary(OpenApiOperation operation)
        {
            static bool ProducesBinary(IEnumerable<string> contentTypes)
            {
                foreach (var p in contentTypes)
                {
                    if (p.Contains("application/json", StringComparison.OrdinalIgnoreCase) ||
                        p.Contains("text/plain", StringComparison.OrdinalIgnoreCase) ||
                        AppJsonRegex.IsMatch(p))
                    {
                        return false;
                    }
                }
                return true;
            }

            static bool IsAllBinarySchemas(IDictionary<string, OpenApiMediaType> content)
            {
                return content.All(c => c.Value?.Schema?.ActualSchema?.IsBinary == true);
            }

            foreach (var r in operation.Responses)
            {
                var key = r.Key;
                var actualResponse = r.Value.ActualResponse;

                if (actualResponse != this || key == "204")
                    continue;

                if (ActualResponse.Content.Count > 0)
                {
                    if (IsAllBinarySchemas(ActualResponse.Content))
                        return true;

                    var contentIsBinary =
                        ActualResponse.Content.All(c =>
                        {
                            var actualSchema = c.Value?.Schema?.ActualSchema;
                            return actualSchema?.IsAnyType != false || actualSchema?.IsBinary != false;
                        }) &&
                        ProducesBinary(ActualResponse.Content.Keys);

                    if (contentIsBinary)
                        return true;
                }

                var actualProduces = (ActualResponse.Parent as OpenApiOperation)?.ActualProduces;
                if (actualProduces?.Count > 0)
                {
                    if (Schema?.ActualSchema.IsBinary == true)
                        return true;

                    var producesIsBinary =
                        (Schema?.ActualSchema.IsAnyType != false || Schema?.ActualSchema.IsBinary != false) &&
                        ProducesBinary(actualProduces);

                    if (producesIsBinary)
                        return true;
                }

                break;
            }

            return false;
        }

        public bool IsEmpty(OpenApiOperation operation)
        {
            return ActualResponse.Content.Count == 0 &&
                   ActualResponse.Schema?.ActualSchema == null &&
                   !IsBinary(operation);
        }

        #region Implementation of IJsonReference

        [JsonIgnore]
        IJsonReference IJsonReference.ActualObject => ActualResponse;

        [JsonIgnore]
        object IJsonReference.PossibleRoot => (Parent as OpenApiOperation)?.Parent?.Parent;

        #endregion
    }
}
