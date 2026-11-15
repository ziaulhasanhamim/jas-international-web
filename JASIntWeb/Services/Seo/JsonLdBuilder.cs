using System.Text.Encodings.Web;
using System.Text.Json;

namespace JASIntWeb.Services.Seo;

public static class JsonLdBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    public static string BuildOrganization(
        string baseUrl,
        string logoUrl,
        IEnumerable<string> alternateNames,
        IEnumerable<string> sameAs,
        string telephone = "+8801711465078",
        string email = "info@jasinternational.com.bd")
    {
        var model = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Organization",
            ["@id"] = $"{baseUrl}/#organization",
            ["name"] = "JAS International",
            ["alternateName"] = alternateNames,
            ["url"] = baseUrl,
            ["logo"] = new Dictionary<string, object>
            {
                ["@type"] = "ImageObject",
                ["url"] = logoUrl,
                ["width"] = 512,
                ["height"] = 512
            },
            ["contactPoint"] = new Dictionary<string, object>
            {
                ["@type"] = "ContactPoint",
                ["telephone"] = telephone,
                ["contactType"] = "Customer Service",
                ["areaServed"] = "BD",
                ["availableLanguage"] = new[] { "Bengali", "English" }
            },
            ["address"] = new Dictionary<string, object>
            {
                ["@type"] = "PostalAddress",
                ["streetAddress"] = "Khulna Shopping Complex, 3rd Floor, Dakbangla",
                ["addressLocality"] = "Khulna",
                ["postalCode"] = "9100",
                ["addressCountry"] = "BD"
            },
            ["sameAs"] = sameAs
        };

        return JsonSerializer.Serialize(model, JsonOptions);
    }

    public static string BuildWebSite(string baseUrl, IEnumerable<string>? alternateNames = null)
    {
        var model = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "WebSite",
            ["@id"] = $"{baseUrl}/#website",
            ["name"] = "JAS International",
            ["alternateName"] = alternateNames ?? ["JAS Gadgets", "JAS Tech", "JAS International Khulna"],
            ["url"] = baseUrl,
            ["potentialAction"] = new Dictionary<string, object>
            {
                ["@type"] = "SearchAction",
                ["target"] = $"{baseUrl}/products?q={{search_term_string}}",
                ["query-input"] = "required name=search_term_string"
            }
        };

        return JsonSerializer.Serialize(model, JsonOptions);
    }

    public static string BuildLocalBusiness(
        string baseUrl,
        string logoUrl,
        string telephone = "+8801711465078")
    {
        var model = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "ElectronicsStore",
            ["@id"] = $"{baseUrl}/#store",
            ["name"] = "JAS International",
            ["image"] = logoUrl,
            ["telephone"] = telephone,
            ["email"] = "info@jasinternational.com.bd",
            ["url"] = baseUrl,
            ["priceRange"] = "৳৳",
            ["currenciesAccepted"] = "BDT",
            ["paymentAccepted"] = "Cash, bKash, Credit Card, Debit Card, Cash on Delivery",
            ["address"] = new Dictionary<string, object>
            {
                ["@type"] = "PostalAddress",
                ["streetAddress"] = "Khulna Shopping Complex, 3rd Floor, Dakbangla",
                ["addressLocality"] = "Khulna",
                ["addressRegion"] = "Khulna Division",
                ["postalCode"] = "9100",
                ["addressCountry"] = "BD"
            },
            ["geo"] = new Dictionary<string, object>
            {
                ["@type"] = "GeoCoordinates",
                ["latitude"] = 22.8157,
                ["longitude"] = 89.5539
            },
            ["openingHoursSpecification"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["@type"] = "OpeningHoursSpecification",
                    ["dayOfWeek"] = new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Saturday", "Sunday" },
                    ["opens"] = "10:00",
                    ["closes"] = "21:00"
                }
            }
        };

        return JsonSerializer.Serialize(model, JsonOptions);
    }

    public static string BuildBreadcrumbs(IEnumerable<(string Name, string Url)> crumbs)
    {
        var list = new List<Dictionary<string, object>>();
        var position = 1;

        foreach (var (name, url) in crumbs)
        {
            list.Add(new Dictionary<string, object>
            {
                ["@type"] = "ListItem",
                ["position"] = position++,
                ["name"] = name,
                ["item"] = url
            });
        }

        var model = new Dictionary<string, object>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "BreadcrumbList",
            ["itemListElement"] = list
        };

        return JsonSerializer.Serialize(model, JsonOptions);
    }

    public static string BuildProduct(
        string name,
        string description,
        string productUrl,
        IEnumerable<string> imageUrls,
        string? brandName,
        string sku,
        string? gtin,
        string? mpn,
        decimal price,
        decimal? compareAtPrice,
        bool inStock,
        decimal shippingKhulna = 80m,
        decimal shippingOutside = 130m,
        decimal? ratingValue = null,
        int? reviewCount = null,
        IEnumerable<(string Author, int Rating, string Comment, DateTimeOffset Date)>? reviews = null)
    {
        var priceValidUntil = DateTime.UtcNow.AddMonths(3).ToString("yyyy-12-31");

        var shippingDetails = new[]
        {
            new Dictionary<string, object>
            {
                ["@type"] = "OfferShippingDetails",
                ["shippingRate"] = new Dictionary<string, object>
                {
                    ["@type"] = "MonetaryAmount",
                    ["value"] = shippingKhulna,
                    ["currency"] = "BDT"
                },
                ["shippingDestination"] = new Dictionary<string, object>
                {
                    ["@type"] = "DefinedRegion",
                    ["addressCountry"] = "BD",
                    ["addressRegion"] = "Khulna"
                },
                ["deliveryTime"] = new Dictionary<string, object>
                {
                    ["@type"] = "ShippingDeliveryTime",
                    ["transitTime"] = new Dictionary<string, object>
                    {
                        ["@type"] = "QuantitativeValue",
                        ["minValue"] = 1,
                        ["maxValue"] = 2,
                        ["unitCode"] = "d"
                    }
                }
            },
            new Dictionary<string, object>
            {
                ["@type"] = "OfferShippingDetails",
                ["shippingRate"] = new Dictionary<string, object>
                {
                    ["@type"] = "MonetaryAmount",
                    ["value"] = shippingOutside,
                    ["currency"] = "BDT"
                },
                ["shippingDestination"] = new Dictionary<string, object>
                {
                    ["@type"] = "DefinedRegion",
                    ["addressCountry"] = "BD"
                },
                ["deliveryTime"] = new Dictionary<string, object>
                {
                    ["@type"] = "ShippingDeliveryTime",
                    ["transitTime"] = new Dictionary<string, object>
                    {
                        ["@type"] = "QuantitativeValue",
                        ["minValue"] = 2,
                        ["maxValue"] = 4,
                        ["unitCode"] = "d"
                    }
                }
            }
        };

        var returnPolicy = new Dictionary<string, object>
        {
            ["@type"] = "MerchantReturnPolicy",
            ["applicableCountry"] = "BD",
            ["returnPolicyCategory"] = "https://schema.org/MerchantReturnFiniteReturnWindow",
            ["merchantReturnDays"] = 7,
            ["returnMethod"] = "https://schema.org/ReturnByMail",
            ["returnFees"] = "https://schema.org/FreeReturn",
            ["refundType"] = "https://schema.org/ExchangeRefund"
        };

        var offer = new Dictionary<string, object?>
        {
            ["@type"] = "Offer",
            ["priceCurrency"] = "BDT",
            ["price"] = price,
            ["priceValidUntil"] = priceValidUntil,
            ["itemCondition"] = "https://schema.org/NewCondition",
            ["availability"] = inStock ? "https://schema.org/InStock" : "https://schema.org/OutOfStock",
            ["url"] = productUrl,
            ["seller"] = new Dictionary<string, object>
            {
                ["@type"] = "Organization",
                ["name"] = "JAS International"
            },
            ["shippingDetails"] = shippingDetails,
            ["hasMerchantReturnPolicy"] = returnPolicy
        };

        var model = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Product",
            ["name"] = name,
            ["description"] = description,
            ["image"] = imageUrls.ToArray(),
            ["url"] = productUrl,
            ["sku"] = sku,
            ["offers"] = offer
        };

        if (!string.IsNullOrWhiteSpace(brandName))
        {
            model["brand"] = new Dictionary<string, object>
            {
                ["@type"] = "Brand",
                ["name"] = brandName
            };
        }

        if (!string.IsNullOrWhiteSpace(gtin)) model["gtin"] = gtin;
        if (!string.IsNullOrWhiteSpace(mpn)) model["mpn"] = mpn;

        // Add aggregateRating and reviews ONLY if valid reviews exist
        if (reviewCount is > 0 && ratingValue is > 0)
        {
            model["aggregateRating"] = new Dictionary<string, object>
            {
                ["@type"] = "AggregateRating",
                ["ratingValue"] = ratingValue.Value,
                ["reviewCount"] = reviewCount.Value,
                ["bestRating"] = 5,
                ["worstRating"] = 1
            };

            if (reviews != null)
            {
                var revList = new List<Dictionary<string, object>>();
                foreach (var r in reviews)
                {
                    revList.Add(new Dictionary<string, object>
                    {
                        ["@type"] = "Review",
                        ["author"] = new Dictionary<string, object>
                        {
                            ["@type"] = "Person",
                            ["name"] = r.Author
                        },
                        ["reviewRating"] = new Dictionary<string, object>
                        {
                            ["@type"] = "Rating",
                            ["ratingValue"] = r.Rating,
                            ["bestRating"] = 5
                        },
                        ["reviewBody"] = r.Comment,
                        ["datePublished"] = r.Date.ToString("yyyy-MM-dd")
                    });
                }
                if (revList.Count > 0)
                {
                    model["review"] = revList;
                }
            }
        }

        return JsonSerializer.Serialize(model, JsonOptions);
    }

    public static string BuildCollection(
        string title,
        string description,
        string url,
        IEnumerable<(string Name, string Url, decimal Price, string? Image)> items)
    {
        var itemList = new List<Dictionary<string, object>>();
        var pos = 1;

        foreach (var item in items)
        {
            var element = new Dictionary<string, object>
            {
                ["@type"] = "Product",
                ["position"] = pos++,
                ["name"] = item.Name,
                ["url"] = item.Url,
                ["offers"] = new Dictionary<string, object>
                {
                    ["@type"] = "Offer",
                    ["priceCurrency"] = "BDT",
                    ["price"] = item.Price
                }
            };
            if (!string.IsNullOrWhiteSpace(item.Image))
            {
                element["image"] = item.Image;
            }
            itemList.Add(element);
        }

        var model = new Dictionary<string, object>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "CollectionPage",
            ["name"] = title,
            ["description"] = description,
            ["url"] = url,
            ["mainEntity"] = new Dictionary<string, object>
            {
                ["@type"] = "ItemList",
                ["itemListElement"] = itemList
            }
        };

        return JsonSerializer.Serialize(model, JsonOptions);
    }

    public static string BuildFaq(IEnumerable<(string Question, string Answer)> faqs)
    {
        var list = new List<Dictionary<string, object>>();
        foreach (var (q, a) in faqs)
        {
            list.Add(new Dictionary<string, object>
            {
                ["@type"] = "Question",
                ["name"] = q,
                ["acceptedAnswer"] = new Dictionary<string, object>
                {
                    ["@type"] = "Answer",
                    ["text"] = a
                }
            });
        }

        var model = new Dictionary<string, object>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "FAQPage",
            ["mainEntity"] = list
        };

        return JsonSerializer.Serialize(model, JsonOptions);
    }

    public static string BuildArticle(
        string title,
        string description,
        string url,
        string? imageUrl,
        string author,
        DateTimeOffset publishedAt,
        DateTimeOffset? updatedAt)
    {
        var model = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Article",
            ["headline"] = title,
            ["description"] = description,
            ["url"] = url,
            ["datePublished"] = publishedAt.ToString("O"),
            ["dateModified"] = (updatedAt ?? publishedAt).ToString("O"),
            ["author"] = new Dictionary<string, object>
            {
                ["@type"] = "Person",
                ["name"] = author
            },
            ["publisher"] = new Dictionary<string, object>
            {
                ["@type"] = "Organization",
                ["name"] = "JAS International",
                ["logo"] = new Dictionary<string, object>
                {
                    ["@type"] = "ImageObject",
                    ["url"] = $"{url.Split("/guides")[0]}/jas-logo.png"
                }
            }
        };

        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            model["image"] = imageUrl;
        }

        return JsonSerializer.Serialize(model, JsonOptions);
    }
}
