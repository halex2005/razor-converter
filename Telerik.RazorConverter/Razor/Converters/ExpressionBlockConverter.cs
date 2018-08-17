using System;
using System.Linq;
using System.Xml.Schema;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Telerik.RazorConverter.Razor.Converters
{
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using Telerik.RazorConverter.Razor.DOM;
    using Telerik.RazorConverter.WebForms.DOM;

    public class ExpressionBlockConverter : INodeConverter<IRazorNode>
    {
        private IRazorExpressionNodeFactory ExpressionNodeFactory
        {
            get;
            set;
        }

        public ExpressionBlockConverter(IRazorExpressionNodeFactory nodeFactory)
        {
            ExpressionNodeFactory = nodeFactory;
        }

        public IList<IRazorNode> ConvertNode(IWebFormsNode node)
        {
            var srcNode = node as IWebFormsExpressionBlockNode;
            var expression = srcNode.Expression.Trim(new char[] { ' ', '\t' });
            expression = expression.Replace("ResolveUrl", "Url.Content");
            expression = RemoveHtmlEncode(expression);
            expression = WrapHtmlDecode(expression);
            var isMultiline = DoesExpressionShouldBeThreatedAsMultiline(expression);
            return new IRazorNode[] 
            {
                ExpressionNodeFactory.CreateExpressionNode(expression, isMultiline)
            };
        }

        private bool DoesExpressionShouldBeThreatedAsMultiline(string expression)
        {
            var options = CSharpParseOptions.Default
                .WithKind(SourceCodeKind.Script)
                .WithDocumentationMode(DocumentationMode.None);
            var syntaxTree = CSharpSyntaxTree.ParseText(expression, options);
            if (!syntaxTree.HasCompilationUnitRoot)
            {
                return false;
            }

            var root = syntaxTree.GetCompilationUnitRoot();
            Func<SyntaxNode, bool> descendIntoChildren = s =>
                !(s is ArgumentListSyntax) &&
                !(s is CastExpressionSyntax) &&
                !(s is ParenthesizedExpressionSyntax);

            int i = 0;
            foreach (var descendantNode in root.DescendantNodes(descendIntoChildren))
            {
                Console.WriteLine($"{i++} {descendantNode.GetType().Name} {descendantNode.Kind().ToString()}");
            }
            
            if (root.DescendantNodes(descendIntoChildren).Any(NodeHasTrivia))
            {
                return true;
            }

            return root.DescendantNodes(descendIntoChildren).Any(node =>
                node is BinaryExpressionSyntax ||
                node is ConditionalExpressionSyntax);
        }

        private static bool NodeHasTrivia(SyntaxNode node)
        {
            if (node is MemberAccessExpressionSyntax)
            {
                return node.ChildTokens().Any(token =>
                    token.HasLeadingTrivia ||
                    token.HasTrailingTrivia ||
                    token.HasStructuredTrivia);
            }
            return
                node.HasLeadingTrivia ||
                node.HasTrailingTrivia ||
                node.HasStructuredTrivia;
        }

        public bool CanConvertNode(IWebFormsNode node)
        {
            return node is IWebFormsExpressionBlockNode;
        }

        private string RemoveHtmlEncode(string input)
        {
            var searchRegex = new Regex(@"(Html\.Encode|HttpUtility\.HtmlEncode)\s*\((?<statement>(?>[^()]+|\((?<Depth>)|\)(?<-Depth>))*(?(Depth)(?!)))\)", RegexOptions.Singleline | RegexOptions.Multiline);
            var stringCastRegex = new Regex(@"^\(\s*string\s*\)\s*", RegexOptions.IgnoreCase);
            return searchRegex.Replace(input, m =>
            {
                return stringCastRegex.Replace(m.Groups["statement"].Value.Trim(), "");
            });
        }

        private string WrapHtmlDecode(string input)
        {
            var searchRegex = new Regex(@"HttpUtility.HtmlDecode\((?<statement>.*)\)", RegexOptions.Singleline | RegexOptions.Multiline);
            return searchRegex.Replace(input, m =>
            {
                return string.Format("Html.Raw(HttpUtility.HtmlDecode({0}))", m.Groups["statement"].Value.Trim());
            });
        }
    }
}
