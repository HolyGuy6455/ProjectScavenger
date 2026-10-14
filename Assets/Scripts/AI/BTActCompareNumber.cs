using System;
using UnityEngine;

[Serializable]
public class BTActCompareNumber : BTActionLogic
{
    public enum ComparisonOperator
    {
        LessThan,
        LessThanOrEqual,
        Equal,
        NotEqual,
        GreaterThanOrEqual,
        GreaterThan
    }

    public BTNumericInput left = new BTNumericInput();
    public ComparisonOperator comparison = ComparisonOperator.LessThan;
    public BTNumericInput right = new BTNumericInput();

    public override string GetDescription(AIBrain brain)
    {
        string symbol = comparison switch
        {
            ComparisonOperator.LessThan => "<",
            ComparisonOperator.LessThanOrEqual => "<=",
            ComparisonOperator.Equal => "==",
            ComparisonOperator.NotEqual => "!=",
            ComparisonOperator.GreaterThanOrEqual => ">=",
            ComparisonOperator.GreaterThan => ">",
            _ => "?"
        };
        return $"{DescribeInput(brain, left)} {symbol} {DescribeInput(brain, right)}이면 Success, 아니면 Failure를 반환합니다.";
    }

    private string DescribeInput(AIBrain brain, BTNumericInput input)
    {
        if (input == null) return "[입력 없음]";
        return input.useDirectValue
            ? $"직접 입력 [{input.directValue}]"
            : $"Blackboard {GetKeyValueString<BTSharedNumeric>(brain, input.blackboardKey)}";
    }

    public override NodeState Evaluate(AIBrain brain)
    {
        if (left == null || right == null
            || !left.TryGetValue(brain, out double leftValue)
            || !right.TryGetValue(brain, out double rightValue))
        {
            Debug.LogError("[BTActCompareNumber] 입력이 없거나 Blackboard 숫자 키가 유효하지 않습니다.");
            return NodeState.FAILURE;
        }

        // 숫자는 double 정밀도를 유지하며, Equal/NotEqual은 오차 허용 없이 비교한다.
        bool conditionMet = comparison switch
        {
            ComparisonOperator.LessThan => leftValue < rightValue,
            ComparisonOperator.LessThanOrEqual => leftValue <= rightValue,
            ComparisonOperator.Equal => leftValue == rightValue,
            ComparisonOperator.NotEqual => leftValue != rightValue,
            ComparisonOperator.GreaterThanOrEqual => leftValue >= rightValue,
            ComparisonOperator.GreaterThan => leftValue > rightValue,
            _ => false
        };
        return conditionMet ? NodeState.SUCCESS : NodeState.FAILURE;
    }
}
