using Moongate.Core.Interfaces.DiceNotation;
using System.Collections.Frozen;
using Moongate.Core.DiceNotation.Exceptions;
using Moongate.Core.DiceNotation.Terms;

namespace Moongate.Core.DiceNotation;

/// <summary>
/// Default class for parsing a string representing a dice expression into a <see cref="DiceExpression" /> instance.
/// </summary>
public class Parser : IParser
{
    // Defines operator priorities
    private static readonly FrozenDictionary<char, int> s_operatorPrecedence =
        new Dictionary<char, int>
        {
            ['('] = 1,
            ['+'] = 2,
            ['-'] = 2,
            ['*'] = 3,
            ['/'] = 3,
            ['k'] = 4,
            ['d'] = 5
        }.ToFrozenDictionary();

    /// <summary>
    /// Parses the dice expression specified into a <see cref="DiceExpression" /> instance.
    /// </summary>
    /// <remarks>
    /// Breaks the dice expression into postfix form, and evaluates the postfix expression to the
    /// degree necessary to produce the appropriate chain of <see cref="ITerm" /> instances.
    /// </remarks>
    /// <param name="expression">The expression to parse.</param>
    /// <returns>
    /// An <see cref="DiceExpression" /> representing the given expression, that can "roll" the expression on command.
    /// </returns>
    public DiceExpression Parse(string expression)
    {
        var postfix = ToPostfix(expression);
        var lastTerm = EvaluatePostfix(postfix);

        // Fails now on what a roll would reject later, such as "1d0" or "5/0".
        lastTerm.GetBounds();

        return new(lastTerm);
    }

    // Evaluates the postfix expression, returning the final term (the one to evaluate to roll
    // the expression)
    private static ITerm EvaluatePostfix(IEnumerable<string> postfix)
    {
        var operands = new Stack<ITerm>();

        foreach (var str in postfix)
        {
            if (char.IsDigit(str[0]) || str.Length > 1) // Operators are all 1 character so > 1 must mean a number like -10
            {
                operands.Push(new ConstantTerm(int.Parse(str)));
            }
            else // Is an operator
            {
                ITerm op1;
                ITerm op2;

                switch (str)
                {
                    case "d":
                        op2 = operands.Pop();
                        op1 = operands.Pop();
                        operands.Push(new DiceTerm(op1, op2));

                        break;

                    case "*":
                        op2 = operands.Pop();
                        op1 = operands.Pop();
                        operands.Push(new MultiplyTerm(op1, op2));

                        break;

                    case "/":
                        op2 = operands.Pop();
                        op1 = operands.Pop();
                        operands.Push(new DivideTerm(op1, op2));

                        break;

                    case "+":
                        op2 = operands.Pop();
                        op1 = operands.Pop();
                        operands.Push(new AddTerm(op1, op2));

                        break;

                    case "-":
                        op2 = operands.Pop();
                        op1 = operands.Pop();
                        operands.Push(new SubtractTerm(op1, op2));

                        break;

                    case "k":
                        op2 = operands.Pop();
                        op1 = operands.Pop();

                        // Must be preceded by a dice term
                        if (op1 is not DiceTerm diceOp)
                        {
                            throw new InvalidSyntaxException();
                        }

                        operands.Push(new KeepTerm(op2, diceOp));

                        break;
                }
            }
        }

        if (operands.Count != 1) // Something went awry
        {
            throw new InvalidSyntaxException();
        }

        return operands.Pop(); // Last thing is the answer!
    }

    // Converts dice notation to postfix notation
    private static IEnumerable<string> ToPostfix(string infix)
    {
        var output = new List<string>();
        var operators = new Stack<char>();

        var charIndex = 0;
        var lastWasOperator = true;

        while (charIndex < infix.Length)
        {
            // Spaces separate tokens and change nothing else: "2 - 3" is a subtraction.
            if (char.IsWhiteSpace(infix[charIndex]))
            {
                charIndex++;

                continue;
            }

            if (char.IsDigit(infix[charIndex]) || lastWasOperator && infix[charIndex] == '-') // Is an operand
            {
                lastWasOperator = false;

                var number = "";
                number += infix[charIndex];
                charIndex++;

                while (charIndex < infix.Length && char.IsDigit(infix[charIndex]))
                {
                    number += infix[charIndex];
                    charIndex++;
                }

                output.Add(number); // Add operand to postfix output
            }
            else // Separate so we can increment charIndex differently
            {
                lastWasOperator = true;

                switch (infix[charIndex])
                {
                    case '(':
                        operators.Push(infix[charIndex]);

                        break;
                    case ')':
                        {
                            if (!operators.Contains('('))
                            {
                                throw new InvalidSyntaxException();
                            }

                            var op = operators.Pop();

                            while (op != '(')
                            {
                                output.Add(op.ToString());
                                op = operators.Pop();
                            }

                            // A closed group is an operand: a '-' after it subtracts.
                            lastWasOperator = false;

                            break;
                        }
                    default:
                        {
                            if (s_operatorPrecedence.ContainsKey(infix[charIndex]))
                            {
                                while (operators.Count > 0 &&
                                       s_operatorPrecedence[operators.Peek()] >=
                                       s_operatorPrecedence[infix[charIndex]])
                                {
                                    output.Add(operators.Pop().ToString());
                                }

                                operators.Push(infix[charIndex]);
                            }
                            else
                            {
                                throw new InvalidSyntaxException();
                            }

                            break;
                        }
                }

                charIndex++;
            }
        }

        while (operators.Count != 0)
        {
            var op = operators.Pop();

            if (op == '(')
            {
                throw new InvalidSyntaxException();
            }

            output.Add(op.ToString());
        }

        return output;
    }
}
