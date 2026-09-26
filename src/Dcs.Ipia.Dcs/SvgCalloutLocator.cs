using System.Xml.Linq;

namespace Dcs.Ipia.Dcs;

/// <summary>Locates authored hardware callout rectangles in draw.io-generated SVGs.</summary>
public static class SvgCalloutLocator {
 #region BEGIN Public
 /// <summary>Maps both MFD box markers and device callout markers to their SVG rectangles.</summary>
 /// <param name="root">The SVG root element.</param>
 /// <returns>Control identities and their authored rectangles.</returns>
 public static IReadOnlyDictionary<string,XElement> Find(XElement root) {
  ArgumentNullException.ThrowIfNull(root);
  var result=new Dictionary<string,XElement>(StringComparer.Ordinal);
  foreach(var comment in root.Nodes().OfType<XComment>()) {
   var marker=comment.Value.Trim();
   var isBox=marker.StartsWith("box:",StringComparison.Ordinal);
   if(!isBox&&!marker.StartsWith("callout:",StringComparison.Ordinal))continue;
   var control=marker[(isBox?"box:":"callout:").Length..].Trim();
   XNode? next=comment.NextNode;
   while(next is XText whitespace&&string.IsNullOrWhiteSpace(whitespace.Value))next=next.NextNode;
   if(!isBox) {
    while(next is XElement element&&element.Name.LocalName is "line" or "polyline" or "circle") {
     next=next.NextNode;
     while(next is XText whitespace&&string.IsNullOrWhiteSpace(whitespace.Value))next=next.NextNode;
    }
   }
   if(next is not XElement rectangle||rectangle.Name.LocalName!="rect")
    throw new InvalidDataException($"Graphic {(isBox?"box":"callout")} {control} has no adjacent rectangle.");
   result.Add(control,rectangle);
  }
  return result;
 }
 #endregion END Public
}
