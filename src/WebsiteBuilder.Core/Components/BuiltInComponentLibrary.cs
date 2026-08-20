using WebsiteBuilder.Core.Models;

namespace WebsiteBuilder.Core.Components;

/// <summary>Version-controlled first-party templates; no runtime code or external files are loaded.</summary>
public static class BuiltInComponentLibrary
{
    public static IReadOnlyList<ComponentDefinition> All { get; } = BuildAndValidate();

    private static IReadOnlyList<ComponentDefinition> BuildAndValidate()
    {
        ComponentDefinition[] definitions =
        [
            BuildNavbar(),
            BuildSimpleHero(),
            BuildSplitHero(),
            BuildFooter(),
            BuildNotFound(),
            BuildPricing(),
            BuildTestimonials(),
            BuildFaq(),
            BuildContactForm(),
            BuildSaasLandingPage(),
            BuildServiceLandingPage(),
        ];
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var definition in definitions)
        {
            ComponentDefinitionValidator.ValidateAndThrow(definition);
            if (!ids.Add(definition.Id))
            {
                throw new InvalidOperationException($"Duplicate component definition id '{definition.Id}'.");
            }
        }

        return Array.AsReadOnly(definitions);
    }

    private static ComponentDefinition BuildSimpleHero() => new(
        "hero-simple",
        "Simple Hero",
        "Hero",
        "Headline, supporting copy, and primary call to action.",
        "H",
        ["hero", "header", "call to action", "landing"],
        new ElementNode
        {
            Id = "tpl-hero-simple",
            Type = ElementTypes.Section,
            Name = "Simple Hero",
            Width = 1000,
            Height = 420,
            Styles =
            {
                ["background"] = "linear-gradient(135deg, #111827, #1e3a8a)",
                ["border-radius"] = "20px",
                ["overflow"] = "hidden",
            },
            ResponsiveStyles =
            {
                ["mobile"] = new()
                {
                    ["left"] = "16px", ["width"] = "448px", ["height"] = "500px", ["border-radius"] = "12px",
                },
            },
            Children =
            {
                new ElementNode
                {
                    Id = "tpl-hero-simple-kicker",
                    Type = ElementTypes.Text,
                    Name = "Eyebrow",
                    X = 64, Y = 64, Width = 360, Height = 24,
                    Text = "DESIGNED IN LIKHA",
                    Styles = { ["color"] = "#93c5fd", ["font-size"] = "13px", ["font-weight"] = "700", ["letter-spacing"] = "1.5px" },
                },
                new ElementNode
                {
                    Id = "tpl-hero-simple-title",
                    Type = ElementTypes.Heading,
                    Name = "Hero Title",
                    X = 64, Y = 104, Width = 760, Height = 112,
                    Text = "Turn an idea into a polished website",
                    Styles = { ["color"] = "#ffffff", ["font-size"] = "48px", ["font-weight"] = "750", ["line-height"] = "1.08" },
                },
                new ElementNode
                {
                    Id = "tpl-hero-simple-copy",
                    Type = ElementTypes.Paragraph,
                    Name = "Hero Copy",
                    X = 64, Y = 232, Width = 650, Height = 62,
                    Text = "Compose responsive sections visually, then export production-ready HTML or Next.js.",
                    Styles = { ["color"] = "#dbeafe", ["font-size"] = "18px", ["line-height"] = "1.55" },
                },
                new ElementNode
                {
                    Id = "tpl-hero-simple-action",
                    Type = ElementTypes.Button,
                    Name = "Primary Action",
                    X = 64, Y = 320, Width = 176, Height = 48,
                    Text = "Start building",
                    Styles =
                    {
                        ["background"] = "#ffffff", ["color"] = "#1e3a8a",
                        ["border-radius"] = "10px", ["font-size"] = "15px", ["font-weight"] = "700",
                        ["display"] = "flex", ["align-items"] = "center", ["justify-content"] = "center",
                    },
                },
            },
        });

    private static ComponentDefinition BuildNavbar() => new(
        "navbar-centered",
        "Centered Navbar",
        "Navigation",
        "Brand, primary navigation links, and a clear call to action.",
        "≡",
        ["navbar", "navigation", "header", "menu"],
        new ElementNode
        {
            Id = "tpl-navbar",
            Type = ElementTypes.Navbar,
            Name = "Centered Navbar",
            Width = 1100,
            Height = 76,
            Styles =
            {
                ["background"] = "#ffffff", ["border"] = "1px solid #e5e7eb",
                ["border-radius"] = "14px", ["box-shadow"] = "0 10px 30px rgba(15, 23, 42, 0.08)",
            },
            ResponsiveStyles = { ["mobile"] = MobileRoot("132px") },
            Children =
            {
                TextNode("tpl-navbar-brand", ElementTypes.Heading, "Brand", 28, 22, 190, 32, "Northstar",
                    ("color", "#0f172a"), ("font-size", "23px"), ("font-weight", "800")),
                LinkNode("tpl-navbar-work", "Work Link", 390, 27, 70, "Work", "#work"),
                LinkNode("tpl-navbar-about", "About Link", 480, 27, 72, "About", "#about"),
                LinkNode("tpl-navbar-contact", "Contact Link", 572, 27, 82, "Contact", "#contact"),
                new ElementNode
                {
                    Id = "tpl-navbar-action", Type = ElementTypes.Link, Name = "Navigation Action",
                    X = 916, Y = 16, Width = 156, Height = 44, Text = "Start a project",
                    Attributes = { ["href"] = "#contact" },
                    Styles =
                    {
                        ["background"] = "#0f172a", ["color"] = "#ffffff", ["border-radius"] = "9px",
                        ["font-size"] = "14px", ["font-weight"] = "700", ["text-decoration"] = "none",
                        ["display"] = "flex", ["align-items"] = "center", ["justify-content"] = "center",
                    },
                    ResponsiveStyles = { ["mobile"] = new() { ["left"] = "28px", ["top"] = "72px" } },
                },
            },
        });

    private static ComponentDefinition BuildSplitHero() => new(
        "hero-split",
        "Split Hero",
        "Hero",
        "Editorial hero with conversion copy and a visual product card.",
        "◧",
        ["hero", "split", "saas", "product", "landing"],
        new ElementNode
        {
            Id = "tpl-split-hero",
            Type = ElementTypes.Section,
            Name = "Split Hero",
            Width = 1100,
            Height = 540,
            Styles = { ["background"] = "#f8fafc", ["border-radius"] = "24px", ["overflow"] = "hidden" },
            ResponsiveStyles = { ["mobile"] = MobileRoot("760px") },
            Children =
            {
                TextNode("tpl-split-kicker", ElementTypes.Text, "Eyebrow", 64, 72, 430, 24, "A CALMER WAY TO SHIP",
                    ("color", "#2563eb"), ("font-size", "13px"), ("font-weight", "750"), ("letter-spacing", "1.4px")),
                new ElementNode
                {
                    Id = "tpl-split-title", Type = ElementTypes.Heading, Name = "Hero Title",
                    X = 64, Y = 112, Width = 500, Height = 154, Text = "Build momentum, not busywork.",
                    Styles = { ["color"] = "#0f172a", ["font-size"] = "54px", ["font-weight"] = "780", ["line-height"] = "1.02" },
                    ResponsiveStyles = { ["mobile"] = new() { ["width"] = "420px", ["font-size"] = "42px" } },
                },
                TextNode("tpl-split-copy", ElementTypes.Paragraph, "Hero Copy", 64, 286, 470, 84,
                    "A focused workspace for teams that want fewer handoffs and more meaningful launches.",
                    ("color", "#475569"), ("font-size", "18px"), ("line-height", "1.55")),
                new ElementNode
                {
                    Id = "tpl-split-action", Type = ElementTypes.Link, Name = "Primary Action",
                    X = 64, Y = 400, Width = 180, Height = 50, Text = "Explore the product",
                    Attributes = { ["href"] = "#product" },
                    Styles =
                    {
                        ["background"] = "#2563eb", ["color"] = "#ffffff", ["border-radius"] = "10px",
                        ["font-size"] = "15px", ["font-weight"] = "700", ["text-decoration"] = "none",
                        ["display"] = "flex", ["align-items"] = "center", ["justify-content"] = "center",
                    },
                },
                new ElementNode
                {
                    Id = "tpl-split-visual", Type = ElementTypes.Card, Name = "Product Preview",
                    X = 620, Y = 58, Width = 416, Height = 424,
                    Styles =
                    {
                        ["background"] = "linear-gradient(160deg, #1e293b, #0f172a)",
                        ["border"] = "1px solid #334155", ["border-radius"] = "22px",
                        ["box-shadow"] = "0 28px 60px rgba(15, 23, 42, 0.22)",
                    },
                    ResponsiveStyles = { ["mobile"] = new() { ["left"] = "64px", ["top"] = "500px", ["width"] = "420px", ["height"] = "210px" } },
                    Children =
                    {
                        TextNode("tpl-split-visual-label", ElementTypes.Text, "Preview Label", 32, 30, 220, 22, "Launch readiness",
                            ("color", "#cbd5e1"), ("font-size", "14px"), ("font-weight", "650")),
                        TextNode("tpl-split-visual-score", ElementTypes.Heading, "Preview Score", 32, 84, 260, 74, "94%",
                            ("color", "#ffffff"), ("font-size", "64px"), ("font-weight", "780")),
                        TextNode("tpl-split-visual-note", ElementTypes.Paragraph, "Preview Note", 32, 180, 330, 62,
                            "Everything important is aligned for this release.",
                            ("color", "#94a3b8"), ("font-size", "16px"), ("line-height", "1.5")),
                    },
                },
            },
        });

    private static ComponentDefinition BuildFooter() => new(
        "footer-multicolumn",
        "Multi-column Footer",
        "Footer",
        "Brand summary, grouped links, and copyright line.",
        "▁",
        ["footer", "navigation", "links", "legal"],
        new ElementNode
        {
            Id = "tpl-footer",
            Type = ElementTypes.Footer,
            Name = "Multi-column Footer",
            Width = 1100,
            Height = 300,
            Styles = { ["background"] = "#0f172a", ["border-radius"] = "20px" },
            ResponsiveStyles = { ["mobile"] = MobileRoot("470px") },
            Children =
            {
                TextNode("tpl-footer-brand", ElementTypes.Heading, "Footer Brand", 52, 50, 240, 36, "Northstar",
                    ("color", "#ffffff"), ("font-size", "26px"), ("font-weight", "800")),
                TextNode("tpl-footer-summary", ElementTypes.Paragraph, "Brand Summary", 52, 104, 330, 70,
                    "Thoughtful digital products for ambitious teams and growing businesses.",
                    ("color", "#94a3b8"), ("font-size", "15px"), ("line-height", "1.55")),
                TextNode("tpl-footer-product", ElementTypes.Text, "Product Heading", 520, 54, 120, 22, "PRODUCT",
                    ("color", "#64748b"), ("font-size", "12px"), ("font-weight", "750"), ("letter-spacing", "1px")),
                LinkNode("tpl-footer-features", "Features Link", 520, 92, 120, "Features", "#features", dark: true),
                LinkNode("tpl-footer-pricing", "Pricing Link", 520, 128, 120, "Pricing", "#pricing", dark: true),
                TextNode("tpl-footer-company", ElementTypes.Text, "Company Heading", 750, 54, 120, 22, "COMPANY",
                    ("color", "#64748b"), ("font-size", "12px"), ("font-weight", "750"), ("letter-spacing", "1px")),
                LinkNode("tpl-footer-about", "About Link", 750, 92, 120, "About", "#about", dark: true),
                LinkNode("tpl-footer-contact", "Contact Link", 750, 128, 120, "Contact", "#contact", dark: true),
                TextNode("tpl-footer-copy", ElementTypes.Text, "Copyright", 52, 244, 500, 22,
                    "© 2026 Northstar. All rights reserved.", ("color", "#64748b"), ("font-size", "13px")),
            },
        });

    private static ComponentDefinition BuildNotFound() => new(
        "page-404-centered",
        "Centered 404",
        "Utility",
        "Friendly not-found state with a route back home.",
        "404",
        ["404", "not found", "error", "utility"],
        new ElementNode
        {
            Id = "tpl-404",
            Type = ElementTypes.Section,
            Name = "Centered 404",
            Width = 1000,
            Height = 560,
            Styles = { ["background"] = "#f8fafc", ["border"] = "1px solid #e2e8f0", ["border-radius"] = "24px" },
            ResponsiveStyles = { ["mobile"] = MobileRoot("560px") },
            Children =
            {
                TextNode("tpl-404-code", ElementTypes.Heading, "Error Code", 300, 86, 400, 120, "404",
                    ("color", "#2563eb"), ("font-size", "108px"), ("font-weight", "850"), ("text-align", "center")),
                TextNode("tpl-404-title", ElementTypes.Heading, "Error Title", 260, 224, 480, 52, "This page wandered off",
                    ("color", "#0f172a"), ("font-size", "36px"), ("font-weight", "760"), ("text-align", "center")),
                TextNode("tpl-404-copy", ElementTypes.Paragraph, "Error Copy", 280, 294, 440, 58,
                    "The link may be outdated, or the page may have moved somewhere new.",
                    ("color", "#64748b"), ("font-size", "16px"), ("line-height", "1.55"), ("text-align", "center")),
                new ElementNode
                {
                    Id = "tpl-404-home", Type = ElementTypes.Link, Name = "Back Home",
                    X = 405, Y = 386, Width = 190, Height = 50, Text = "Back to homepage",
                    Attributes = { ["href"] = "index.html" },
                    Styles =
                    {
                        ["background"] = "#0f172a", ["color"] = "#ffffff", ["border-radius"] = "10px",
                        ["font-size"] = "15px", ["font-weight"] = "700", ["text-decoration"] = "none",
                        ["display"] = "flex", ["align-items"] = "center", ["justify-content"] = "center",
                    },
                },
            },
        });

    private static ComponentDefinition BuildPricing() => new(
        "pricing-three-tier",
        "Three-tier Pricing",
        "Pricing",
        "Three clear plans with a highlighted recommended tier.",
        "$",
        ["pricing", "plans", "subscription", "saas", "conversion"],
        new ElementNode
        {
            Id = "tpl-pricing",
            Type = ElementTypes.Section,
            Name = "Three-tier Pricing",
            Width = 1100,
            Height = 700,
            Styles = { ["background"] = "#f8fafc", ["border-radius"] = "24px" },
            ResponsiveStyles = { ["mobile"] = MobileRoot("1500px") },
            Children =
            {
                TextNode("tpl-pricing-title", ElementTypes.Heading, "Pricing Title", 230, 58, 640, 56,
                    "Pricing that grows with you", ("color", "#0f172a"), ("font-size", "40px"),
                    ("font-weight", "780"), ("text-align", "center")),
                TextNode("tpl-pricing-copy", ElementTypes.Paragraph, "Pricing Copy", 280, 126, 540, 52,
                    "Start small, upgrade when the work demands it, and cancel whenever you need.",
                    ("color", "#64748b"), ("font-size", "16px"), ("line-height", "1.5"), ("text-align", "center")),
                PricingCard("starter", 52, 216, 32, "Starter", "$12", "For personal projects", false),
                PricingCard("growth", 386, 196, 470, "Growth", "$29", "For teams shipping weekly", true),
                PricingCard("scale", 720, 216, 908, "Scale", "$79", "For ambitious organizations", false),
            },
        });

    private static ComponentDefinition BuildTestimonials() => new(
        "testimonials-three-card",
        "Three Testimonials",
        "Social Proof",
        "A headline and three concise customer stories.",
        "❝",
        ["testimonials", "reviews", "quotes", "customers", "social proof"],
        new ElementNode
        {
            Id = "tpl-testimonials",
            Type = ElementTypes.Section,
            Name = "Three Testimonials",
            Width = 1100,
            Height = 560,
            Styles = { ["background"] = "#0f172a", ["border-radius"] = "24px" },
            ResponsiveStyles = { ["mobile"] = MobileRoot("1080px") },
            Children =
            {
                TextNode("tpl-testimonials-kicker", ElementTypes.Text, "Section Label", 72, 60, 280, 22,
                    "CUSTOMER STORIES", ("color", "#60a5fa"), ("font-size", "12px"),
                    ("font-weight", "750"), ("letter-spacing", "1.3px")),
                TextNode("tpl-testimonials-title", ElementTypes.Heading, "Section Title", 72, 98, 700, 60,
                    "Trusted by teams who care about craft", ("color", "#ffffff"),
                    ("font-size", "38px"), ("font-weight", "760")),
                TestimonialCard("maya", 54, 208, 208, "“We moved from scattered drafts to a launch-ready page in an afternoon.”", "Maya Chen", "Product lead"),
                TestimonialCard("jon", 386, 208, 490, "“The exported code was clean enough that our developers picked it up immediately.”", "Jon Bell", "Engineering manager"),
                TestimonialCard("ana", 718, 208, 772, "“Responsive editing finally feels direct instead of being a chain of compromises.”", "Ana Reyes", "Independent designer"),
            },
        });

    private static ComponentDefinition BuildFaq() => new(
        "faq-stacked",
        "Stacked FAQ",
        "FAQ",
        "Four answer cards for common product or service questions.",
        "?",
        ["faq", "questions", "answers", "support", "accordion"],
        new ElementNode
        {
            Id = "tpl-faq",
            Type = ElementTypes.Section,
            Name = "Stacked FAQ",
            Width = 1000,
            Height = 720,
            Styles = { ["background"] = "#ffffff", ["border"] = "1px solid #e2e8f0", ["border-radius"] = "22px" },
            ResponsiveStyles = { ["mobile"] = MobileRoot("720px") },
            Children =
            {
                TextNode("tpl-faq-title", ElementTypes.Heading, "FAQ Title", 64, 54, 560, 54,
                    "Questions, answered", ("color", "#0f172a"), ("font-size", "38px"), ("font-weight", "780")),
                TextNode("tpl-faq-copy", ElementTypes.Paragraph, "FAQ Intro", 64, 116, 650, 52,
                    "Everything you need to know before starting your next project.",
                    ("color", "#64748b"), ("font-size", "16px"), ("line-height", "1.5")),
                FaqItem("first", 200, "Can I edit every part of a block?", "Yes. Once inserted, a block is an ordinary element tree. Rename, restyle, move, or remove any child."),
                FaqItem("second", 320, "Will the design remain responsive?", "Built-in blocks include mobile overrides, and you can refine each breakpoint in the inspector."),
                FaqItem("third", 440, "Which export formats are supported?", "Generate a static HTML site or a strict Next.js App Router project from the same design."),
                FaqItem("fourth", 560, "Can I use my own assets and fonts?", "Import validated project assets, drag media into the canvas, and apply managed fonts to any selection."),
            },
        });

    private static ComponentDefinition BuildContactForm() => new(
        "form-contact",
        "Contact Form",
        "Forms",
        "Accessible contact form layout with name, email, message, and submit action.",
        "✉",
        ["form", "contact", "lead", "email", "message"],
        new ElementNode
        {
            Id = "tpl-contact",
            Type = ElementTypes.Section,
            Name = "Contact Form Section",
            Width = 1000,
            Height = 650,
            Styles = { ["background"] = "linear-gradient(145deg, #eff6ff, #f8fafc)", ["border-radius"] = "24px" },
            ResponsiveStyles = { ["mobile"] = MobileRoot("900px") },
            Children =
            {
                TextNode("tpl-contact-title", ElementTypes.Heading, "Contact Title", 58, 70, 360, 108,
                    "Tell us what you want to build", ("color", "#0f172a"), ("font-size", "42px"),
                    ("font-weight", "780"), ("line-height", "1.12")),
                TextNode("tpl-contact-copy", ElementTypes.Paragraph, "Contact Copy", 58, 198, 350, 96,
                    "Share the goal, the timeline, and what success looks like. We will reply within two business days.",
                    ("color", "#64748b"), ("font-size", "16px"), ("line-height", "1.6")),
                new ElementNode
                {
                    Id = "tpl-contact-form", Type = ElementTypes.Form, Name = "Contact Form",
                    X = 486, Y = 54, Width = 456, Height = 542,
                    Attributes = { ["method"] = "post" },
                    Styles =
                    {
                        ["background"] = "#ffffff", ["border"] = "1px solid #dbeafe",
                        ["border-radius"] = "18px", ["box-shadow"] = "0 24px 50px rgba(37, 99, 235, 0.12)",
                    },
                    ResponsiveStyles = { ["mobile"] = new() { ["left"] = "58px", ["top"] = "310px" } },
                    Children =
                    {
                        TextNode("tpl-contact-name-label", ElementTypes.Text, "Name Label", 32, 30, 180, 22, "Name",
                            ("color", "#334155"), ("font-size", "14px"), ("font-weight", "650")),
                        InputNode("tpl-contact-name", "Name Input", 32, 60, 392, "name", "Your name", "text"),
                        TextNode("tpl-contact-email-label", ElementTypes.Text, "Email Label", 32, 132, 180, 22, "Email",
                            ("color", "#334155"), ("font-size", "14px"), ("font-weight", "650")),
                        InputNode("tpl-contact-email", "Email Input", 32, 162, 392, "email", "you@example.com", "email"),
                        TextNode("tpl-contact-message-label", ElementTypes.Text, "Message Label", 32, 234, 180, 22, "Message",
                            ("color", "#334155"), ("font-size", "14px"), ("font-weight", "650")),
                        new ElementNode
                        {
                            Id = "tpl-contact-message", Type = ElementTypes.Textarea, Name = "Message Input",
                            X = 32, Y = 264, Width = 392, Height = 130,
                            Attributes = { ["name"] = "message", ["placeholder"] = "Tell us about the project", ["required"] = "true" },
                            Styles =
                            {
                                ["background"] = "#ffffff", ["border"] = "1px solid #cbd5e1", ["border-radius"] = "9px",
                                ["color"] = "#0f172a", ["font-size"] = "15px", ["padding"] = "12px", ["resize"] = "none",
                            },
                        },
                        new ElementNode
                        {
                            Id = "tpl-contact-submit", Type = ElementTypes.Button, Name = "Submit Button",
                            X = 32, Y = 426, Width = 392, Height = 50, Text = "Send inquiry",
                            Attributes = { ["type"] = "submit" },
                            Styles =
                            {
                                ["background"] = "#2563eb", ["border"] = "0", ["border-radius"] = "9px",
                                ["color"] = "#ffffff", ["font-size"] = "15px", ["font-weight"] = "700",
                            },
                        },
                    },
                },
            },
        });

    private static ComponentDefinition BuildSaasLandingPage()
    {
        var navbar = Place(BuildNavbar().Root, 50, 28, 28);
        var hero = Place(BuildSplitHero().Root, 50, 132, 190);
        var pricing = Place(BuildPricing().Root, 50, 720, 1000);
        var faq = Place(BuildFaq().Root, 100, 1470, 2550);
        var footer = Place(BuildFooter().Root, 50, 2240, 3330);
        return new ComponentDefinition(
            "landing-saas",
            "SaaS Landing Page",
            "Landing Pages",
            "Complete product landing page with navigation, split hero, pricing, FAQ, and footer.",
            "S",
            ["landing page", "saas", "startup", "product", "pricing"],
            new ElementNode
            {
                Id = "tpl-landing-saas",
                Type = ElementTypes.Container,
                Name = "SaaS Landing Page",
                Width = 1200,
                Height = 2600,
                Styles = { ["background"] = "#ffffff" },
                ResponsiveStyles = { ["mobile"] = new() { ["left"] = "0", ["width"] = "480px", ["height"] = "3950px" } },
                Children = { navbar, hero, pricing, faq, footer },
            });
    }

    private static ComponentDefinition BuildServiceLandingPage()
    {
        var navbar = Place(BuildNavbar().Root, 50, 28, 28);
        var hero = Place(BuildSimpleHero().Root, 100, 132, 190);
        var testimonials = Place(BuildTestimonials().Root, 50, 602, 740);
        var contact = Place(BuildContactForm().Root, 100, 1212, 1870);
        var footer = Place(BuildFooter().Root, 50, 1912, 2820);
        return new ComponentDefinition(
            "landing-services",
            "Services Landing Page",
            "Landing Pages",
            "Complete services page with hero, proof, contact form, and footer.",
            "L",
            ["landing page", "services", "agency", "portfolio", "contact"],
            new ElementNode
            {
                Id = "tpl-landing-services",
                Type = ElementTypes.Container,
                Name = "Services Landing Page",
                Width = 1200,
                Height = 2290,
                Styles = { ["background"] = "#ffffff" },
                ResponsiveStyles = { ["mobile"] = new() { ["left"] = "0", ["width"] = "480px", ["height"] = "3400px" } },
                Children = { navbar, hero, testimonials, contact, footer },
            });
    }

    private static Dictionary<string, string> MobileRoot(string height) => new(StringComparer.Ordinal)
    {
        ["left"] = "16px",
        ["width"] = "448px",
        ["height"] = height,
    };

    private static ElementNode Place(ElementNode node, double x, double y, double? mobileTop = null)
    {
        node.X = x;
        node.Y = y;
        if (mobileTop is not null)
        {
            if (!node.ResponsiveStyles.TryGetValue("mobile", out var styles))
            {
                styles = new Dictionary<string, string>(StringComparer.Ordinal);
                node.ResponsiveStyles["mobile"] = styles;
            }

            styles["top"] = $"{mobileTop.Value}px";
        }

        return node;
    }

    private static ElementNode PricingCard(
        string suffix,
        double x,
        double y,
        double mobileTop,
        string title,
        string price,
        string description,
        bool featured) => new()
        {
            Id = $"tpl-pricing-{suffix}",
            Type = ElementTypes.Card,
            Name = $"{title} Plan",
            X = x,
            Y = y,
            Width = 310,
            Height = featured ? 430 : 410,
            Styles =
            {
                ["background"] = featured ? "#1e3a8a" : "#ffffff",
                ["border"] = featured ? "1px solid #2563eb" : "1px solid #e2e8f0",
                ["border-radius"] = "18px",
                ["box-shadow"] = featured ? "0 24px 50px rgba(37, 99, 235, 0.2)" : "0 12px 30px rgba(15, 23, 42, 0.06)",
            },
            ResponsiveStyles = { ["mobile"] = new() { ["left"] = "52px", ["top"] = $"{mobileTop}px" } },
            Children =
            {
                TextNode($"tpl-pricing-{suffix}-name", ElementTypes.Heading, "Plan Name", 28, 30, 240, 34, title,
                    ("color", featured ? "#ffffff" : "#0f172a"), ("font-size", "22px"), ("font-weight", "750")),
                TextNode($"tpl-pricing-{suffix}-price", ElementTypes.Heading, "Plan Price", 28, 88, 240, 62, price,
                    ("color", featured ? "#bfdbfe" : "#2563eb"), ("font-size", "48px"), ("font-weight", "800")),
                TextNode($"tpl-pricing-{suffix}-copy", ElementTypes.Paragraph, "Plan Description", 28, 164, 250, 56, description,
                    ("color", featured ? "#cbd5e1" : "#64748b"), ("font-size", "15px"), ("line-height", "1.5")),
                TextNode($"tpl-pricing-{suffix}-features", ElementTypes.Paragraph, "Plan Features", 28, 238, 250, 82,
                    "✓ Unlimited drafts\n✓ Responsive export\n✓ Project assets",
                    ("color", featured ? "#dbeafe" : "#334155"), ("font-size", "14px"), ("line-height", "1.8"), ("white-space", "pre-line")),
                new ElementNode
                {
                    Id = $"tpl-pricing-{suffix}-action", Type = ElementTypes.Link, Name = "Choose Plan",
                    X = 28, Y = featured ? 350 : 330, Width = 254, Height = 48, Text = featured ? "Choose Growth" : $"Choose {title}",
                    Attributes = { ["href"] = "#contact" },
                    Styles =
                    {
                        ["background"] = featured ? "#ffffff" : "#0f172a", ["color"] = featured ? "#1e3a8a" : "#ffffff",
                        ["border-radius"] = "9px", ["font-size"] = "14px", ["font-weight"] = "700",
                        ["text-decoration"] = "none", ["display"] = "flex", ["align-items"] = "center", ["justify-content"] = "center",
                    },
                },
            },
        };

    private static ElementNode TestimonialCard(
        string suffix,
        double x,
        double y,
        double mobileTop,
        string quote,
        string author,
        string role) => new()
        {
            Id = $"tpl-testimonial-{suffix}",
            Type = ElementTypes.Card,
            Name = $"{author} Testimonial",
            X = x,
            Y = y,
            Width = 312,
            Height = 278,
            Styles = { ["background"] = "#1e293b", ["border"] = "1px solid #334155", ["border-radius"] = "16px" },
            ResponsiveStyles = { ["mobile"] = new() { ["left"] = "54px", ["top"] = $"{mobileTop}px" } },
            Children =
            {
                TextNode($"tpl-testimonial-{suffix}-mark", ElementTypes.Text, "Quote Mark", 24, 22, 50, 42, "“",
                    ("color", "#60a5fa"), ("font-size", "42px"), ("font-weight", "800")),
                TextNode($"tpl-testimonial-{suffix}-quote", ElementTypes.Paragraph, "Quote", 24, 76, 264, 112, quote,
                    ("color", "#e2e8f0"), ("font-size", "16px"), ("line-height", "1.55")),
                TextNode($"tpl-testimonial-{suffix}-author", ElementTypes.Text, "Author", 24, 214, 180, 22, author,
                    ("color", "#ffffff"), ("font-size", "14px"), ("font-weight", "700")),
                TextNode($"tpl-testimonial-{suffix}-role", ElementTypes.Text, "Role", 24, 240, 220, 20, role,
                    ("color", "#64748b"), ("font-size", "13px")),
            },
        };

    private static ElementNode FaqItem(string suffix, double y, string question, string answer) => new()
    {
        Id = $"tpl-faq-{suffix}",
        Type = ElementTypes.Card,
        Name = question,
        X = 64,
        Y = y,
        Width = 872,
        Height = 96,
        Styles = { ["background"] = "#f8fafc", ["border"] = "1px solid #e2e8f0", ["border-radius"] = "12px" },
        Children =
        {
            TextNode($"tpl-faq-{suffix}-question", ElementTypes.Heading, "Question", 22, 16, 820, 28, question,
                ("color", "#0f172a"), ("font-size", "17px"), ("font-weight", "700")),
            TextNode($"tpl-faq-{suffix}-answer", ElementTypes.Paragraph, "Answer", 22, 48, 820, 40, answer,
                ("color", "#64748b"), ("font-size", "14px"), ("line-height", "1.45")),
        },
    };

    private static ElementNode InputNode(
        string id,
        string name,
        double x,
        double y,
        double width,
        string fieldName,
        string placeholder,
        string type) => new()
        {
            Id = id,
            Type = ElementTypes.Input,
            Name = name,
            X = x,
            Y = y,
            Width = width,
            Height = 46,
            Attributes = { ["name"] = fieldName, ["placeholder"] = placeholder, ["type"] = type, ["required"] = "true" },
            Styles =
            {
                ["background"] = "#ffffff", ["border"] = "1px solid #cbd5e1", ["border-radius"] = "9px",
                ["color"] = "#0f172a", ["font-size"] = "15px", ["padding"] = "0 12px",
            },
        };

    private static ElementNode TextNode(
        string id,
        string type,
        string name,
        double x,
        double y,
        double width,
        double height,
        string text,
        params (string Name, string Value)[] styles) => new()
        {
            Id = id,
            Type = type,
            Name = name,
            X = x,
            Y = y,
            Width = width,
            Height = height,
            Text = text,
            Styles = styles.ToDictionary(style => style.Name, style => style.Value, StringComparer.Ordinal),
        };

    private static ElementNode LinkNode(
        string id,
        string name,
        double x,
        double y,
        double width,
        string text,
        string href,
        bool dark = false) => new()
        {
            Id = id,
            Type = ElementTypes.Link,
            Name = name,
            X = x,
            Y = y,
            Width = width,
            Height = 24,
            Text = text,
            Attributes = { ["href"] = href },
            Styles =
            {
                ["color"] = dark ? "#cbd5e1" : "#475569",
                ["font-size"] = "14px", ["font-weight"] = "600", ["text-decoration"] = "none",
            },
        };
}
