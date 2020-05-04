﻿using DatabaseConverter.Model;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TSQL;
using TSQL.Tokens;

namespace DatabaseConverter.Core
{
    public class DbObjectTokenTranslator : DbObjectTranslator
    {
        private List<string> dataTypes = new List<string>();

        private List<FunctionSpecification> sourceFuncSpecs;
        private List<FunctionSpecification> targetFuncSpecs;

        public DbObjectTokenTranslator(DbInterpreter source, DbInterpreter target) : base(source, target) { }

        public override void Translate()
        {

        }

        public virtual string ParseDefinition(string definition)
        {
            var tokens = this.GetTokens(definition);
            bool changed = false;

            definition = this.HandleDefinition(definition, tokens, out changed);

            if (changed)
            {
                tokens = this.GetTokens(definition);
            }

            definition = this.BuildDefinition(tokens);

            return definition;
        }

        protected string HandleDefinition(string definition, List<TSQLToken> tokens, out bool changed)
        {
            this.sourceFuncSpecs = FunctionManager.GetFunctionSpecifications(this.sourceDbInterpreter.DatabaseType);
            this.targetFuncSpecs = FunctionManager.GetFunctionSpecifications(this.targetDbInterpreter.DatabaseType);

            changed = false;

            string newDefinition = definition;

            foreach (TSQLToken token in tokens)
            {
                string text = token.Text;
                string functionExpression = null;

                switch (token.Type)
                {
                    case TSQLTokenType.SystemIdentifier:

                        functionExpression = this.GetFunctionExpression(token, definition);

                        break;

                    case TSQLTokenType.Identifier:

                        switch (text.ToUpper())
                        {
                            case "CAST":

                                functionExpression = this.GetFunctionExpression(token, definition);

                                break;
                        }
                        break;

                    case TSQLTokenType.Keyword:

                        break;
                }

                if (!string.IsNullOrEmpty(functionExpression))
                {
                    string targetFunctionName = this.GetMappedFunctionName(text);

                    FunctionFomular fomular = new FunctionFomular(functionExpression);

                    Dictionary<string, string> dictDataType = null;

                    string newExpression = this.ParseFomular(this.sourceFuncSpecs, this.targetFuncSpecs, fomular, targetFunctionName, out dictDataType);

                    if (newExpression != fomular.Expression)
                    {
                        newDefinition = this.ReplaceValue(newDefinition, fomular.Expression, newExpression);

                        changed = true;
                    }

                    if (dictDataType != null)
                    {
                        this.dataTypes.AddRange(dictDataType.Values);
                    }
                }
            }

            return newDefinition;
        }

        private string GetFunctionExpression(TSQLToken token, string definition)
        {
            int startIndex = startIndex = token.BeginPosition;
            int functionEndIndex = functionEndIndex = this.FindFunctionEndIndex(startIndex + token.Text.Length, definition);

            string functionExpression = null;

            if (functionEndIndex != -1)
            {
                functionExpression = definition.Substring(startIndex, functionEndIndex - startIndex + 1);
            }

            return functionExpression;
        }

        private int FindFunctionEndIndex(int startIndex, string definition)
        {
            int leftBracketCount = 0;
            int rightBracketCount = 0;
            int functionEndIndex = -1;

            for (int i = startIndex; i < definition.Length; i++)
            {
                if (definition[i] == '(')
                {
                    leftBracketCount++;
                }
                else if (definition[i] == ')')
                {
                    rightBracketCount++;
                }

                if (rightBracketCount == leftBracketCount)
                {
                    functionEndIndex = i;
                    break;
                }
            }

            return functionEndIndex;
        }

        public string BuildDefinition(List<TSQL.Tokens.TSQLToken> tokens)
        {
            StringBuilder sb = new StringBuilder();

            this.sourceOwnerName = DbInterpreterHelper.GetOwnerName(sourceDbInterpreter);

            int ignoreCount = 0;

            TSQLTokenType previousType = TSQLTokenType.Whitespace;
            string previousText = "";

            for (int i = 0; i < tokens.Count; i++)
            {
                if (ignoreCount > 0)
                {
                    ignoreCount--;
                    continue;
                }

                var token = tokens[i];

                var tokenType = token.Type;
                string text = token.Text;

                switch (tokenType)
                {
                    case TSQLTokenType.Identifier:

                        var nextToken = i + 1 < tokens.Count ? tokens[i + 1] : null;

                        if (this.sourceDbInterpreter.DatabaseType == DatabaseType.SqlServer)
                        {
                            if ((text == "dbo" || text == "[dbo]") && this.TargetDbOwner?.ToLower() != "dbo")
                            {
                                if(nextToken!=null && nextToken.Text==".")
                                {
                                    ignoreCount++;
                                }

                                continue;
                            }
                        }

                        if (dataTypes.Contains(text))
                        {
                            sb.Append(text);
                            continue;
                        }                       

                        //Remove owner name
                        if (nextToken != null && nextToken.Text.Trim() != "(" &&
                            text.Trim('"') == sourceOwnerName && i + 1 < tokens.Count && tokens[i + 1].Text == "."
                            )
                        {
                            ignoreCount++;
                            continue;
                        }
                        else if (nextToken != null && nextToken.Text.Trim() == "(") //function handle
                        {
                            string textWithBrackets = text.ToLower() + "()";

                            bool useBrackets = false;

                            if (this.functionMappings.Any(item => item.Any(t => t.Function.ToLower() == textWithBrackets)))
                            {
                                useBrackets = true;
                                text = textWithBrackets;
                            }

                            IEnumerable<FunctionMapping> funcMappings = this.functionMappings.FirstOrDefault(item => item.Any(t => t.DbType == sourceDbInterpreter.DatabaseType.ToString() && t.Function.Split(',').Any(m => m.ToLower() == text.ToLower())));

                            if (funcMappings != null)
                            {
                                string targetFunction = funcMappings.FirstOrDefault(item => item.DbType == targetDbInterpreter.DatabaseType.ToString())?.Function.Split(',')?.FirstOrDefault();

                                if (!string.IsNullOrEmpty(targetFunction))
                                {
                                    sb.Append(targetFunction);
                                }

                                if (useBrackets)
                                {
                                    ignoreCount += 2;
                                }
                            }
                            else
                            {
                                if (text.StartsWith(this.sourceDbInterpreter.QuotationLeftChar.ToString()) && text.EndsWith(this.sourceDbInterpreter.QuotationRightChar.ToString()))
                                {
                                    sb.Append(this.GetQuotedString(text.Trim(this.sourceDbInterpreter.QuotationLeftChar, this.sourceDbInterpreter.QuotationRightChar)));
                                }
                                else
                                {
                                    sb.Append(text);
                                }
                            }
                        }
                        else
                        {
                            sb.Append(this.GetQuotedString(text));
                        }
                        break;
                    case TSQLTokenType.StringLiteral:
                        if (previousType != TSQLTokenType.Whitespace && previousText.ToLower() == "as")
                        {
                            sb.Append(this.GetQuotedString(text));
                        }
                        else
                        {
                            sb.Append(text);
                        }
                        break;
                    case TSQLTokenType.SingleLineComment:
                    case TSQLTokenType.MultilineComment:
                        continue;
                    case TSQLTokenType.Keyword:
                        switch (text.ToUpper())
                        {
                            case "AS":
                                if (targetDbInterpreter is OracleInterpreter)
                                {
                                    var previousKeyword = (from t in tokens where t.Type == TSQLTokenType.Keyword && t.EndPosition < token.BeginPosition select t).LastOrDefault();
                                    if (previousKeyword != null && previousKeyword.Text.ToUpper() == "FROM")
                                    {
                                        continue;
                                    }
                                }
                                break;
                        }
                        sb.Append(text);
                        break;
                    default:
                        sb.Append(text);
                        break;
                }

                if (!string.IsNullOrWhiteSpace(text))
                {
                    previousText = text;
                    previousType = tokenType;
                }
            }

            return sb.ToString();
        }

        private string GetQuotedString(string text)
        {
            if (//text.StartsWith(this.sourceDbInterpreter.QuotationLeftChar.ToString()) && text.EndsWith(this.sourceDbInterpreter.QuotationRightChar.ToString())&& 
                !text.StartsWith(this.targetDbInterpreter.QuotationLeftChar.ToString()) && !text.EndsWith(this.targetDbInterpreter.QuotationRightChar.ToString()))
            {
                return this.targetDbInterpreter.GetQuotedString(text.Trim('\'', '"', this.sourceDbInterpreter.QuotationLeftChar, this.sourceDbInterpreter.QuotationRightChar));
            }

            return text;
        }

        public List<TSQL.Tokens.TSQLToken> GetTokens(string sql)
        {
            return TSQLTokenizer.ParseTokens(sql, true, true);
        }
    }
}
