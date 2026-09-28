using System.Xml.Linq;
using Dcs.Ipia.Dcs;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Checks draw.io marker shapes used by shared hardware SVGs.</summary>
    public sealed class SvgCalloutLocatorTests
    {
        #region BEGIN Public
        /// <summary>The AVA alias uses the Warthog grip SVG, whose box markers lead directly to text.</summary>
        [Fact]
        public void Text_only_ava_callouts_keep_their_labels_and_selection_regions()
        {
            const string svg = """
               <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 960 580">
                 <!-- box:warthog-grip-btn-red -->
                 <text id="lbl-warthog-grip-btn-red" x="462" y="32" text-anchor="middle">Weapon release</text>
                 <!-- box:warthog-grip-tms-up -->
                 <text id="lbl-warthog-grip-tms-up" x="363" y="70" text-anchor="middle">TMS up</text>
               </svg>
               """;
            var root = XDocument.Parse(svg, LoadOptions.PreserveWhitespace).Root!;
            var boxes = SvgCalloutLocator.Find(root);
            Assert.Equal(2, boxes.Count);
            var box = boxes["warthog-grip-btn-red"];
            Assert.Equal("420", box.Attribute("x")?.Value);
            Assert.Equal("14", box.Attribute("y")?.Value);
            Assert.Equal("84", box.Attribute("width")?.Value);
            Assert.Equal(
                "Weapon release",
                Assert.Single(box.ElementsAfterSelf(), x => x.Attribute("id")?.Value == "lbl-warthog-grip-btn-red")
                    .Value);
            Assert.Equal("321", boxes["warthog-grip-tms-up"].Attribute("x")?.Value);
        }
        /// <summary>A text-only marker must identify the same control and have usable coordinates.</summary>
        [Fact]
        public void Rejects_unrelated_or_unpositioned_labels()
        {
            var unrelated =
                XDocument.Parse("<svg><!-- box:one --><text id='lbl-two' x='20' y='30'>Other</text></svg>").Root!;
            Assert.Throws<InvalidDataException>(() => SvgCalloutLocator.Find(unrelated));
            var unpositioned = XDocument.Parse("<svg><!-- box:one --><text id='lbl-one' y='30'>One</text></svg>").Root!;
            Assert.Throws<InvalidDataException>(() => SvgCalloutLocator.Find(unpositioned));
        }
        #endregion END Public
    }
}
