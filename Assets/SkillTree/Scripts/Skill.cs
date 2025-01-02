using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;
using System;

public class Skill : MonoBehaviour
{
    private SkillTree skillTree;

    public string title;

    public enum SkillState
    {
        Invisible,
        Visible,
        Unlockable,
        Active,
        Locked
    }

    public SkillState state;

    public int currentLevel;
    public List<SkillLevel> levels;
    public bool capped;

    public List<Skill> parentSkills;
    public List<Skill> childSkills;

    public List<SkillRequirement> visibilityRequirements;
    public List<SkillRequirement> unlockableRequirements;

    private void Awake()
    {
        if (levels == null || levels.Count == 0)
        {
            levels = new List<SkillLevel>();
            levels.Add(new SkillLevel { cost = 0 }); // Add a default SkillLevel
        }
    }

    private void Start()
    {
        skillTree = GameObject.FindFirstObjectByType<SkillTree>();
    }

    public void Fire()
    {
        UpdateSkill();
        UpdateVisual();
        foreach(Skill skill in childSkills)
        {
            skill.UpdateVisual();
        }
    }

    public void UpdateSkill()
    {
        switch (state)
        {
            case SkillState.Invisible:
                break;
            case SkillState.Visible:
                break;
            case SkillState.Unlockable:
                if (CanBuy()) ToActive();
                break;
            case SkillState.Active:
                if (!capped && CanBuy()) LevelUp();
                break;
            case SkillState.Locked:

                break;
            default:

                break;
        }
    }

    public void UpdateVisual()
    {
        switch (state)
        {
            case SkillState.Invisible:
                if (CheckRequirements(visibilityRequirements)) ToVisible();
                break;
            case SkillState.Visible:
                if (CheckRequirements(unlockableRequirements)) ToUnlockable();
                break;
            case SkillState.Unlockable:

                break;
            case SkillState.Active:

                break;
            case SkillState.Locked:

                break;
            default:

                break;
        }
    }

    private void ToVisible()
    {
        state = SkillState.Visible;
        Debug.Log(title + ": became visible");
    }

    private void ToUnlockable()
    {
        state = SkillState.Unlockable;
        Debug.Log(title + ": became unlockable");
    }

    private void ToActive()
    {
        currentLevel = 1;
        if (currentLevel >= levels.Count) capped = true;
        state = SkillState.Active;
        Debug.Log(title + ": became active");
    }

    private void LevelUp()
    {
        currentLevel++;
        if (currentLevel >= levels.Count) capped = true;
        Debug.Log(title + ": leveld up");
    }

    private bool CanBuy()
    {
        switch (levels[currentLevel].currencyType)
        {
            case SkillTree.CurrencyTypes.normal:
                if (skillTree.normalCurrency < levels[currentLevel].cost) return false;
                break;
            case SkillTree.CurrencyTypes.special:
                break;
            case SkillTree.CurrencyTypes.super:
                break;
            default:
                break;
        }
        return true;
    }

    private bool CheckRequirements(List<SkillRequirement> requirements)
    {
        foreach (SkillRequirement requirement in requirements)
        {
            switch (requirement.requirementType)
            {
                case SkillRequirement.RequirementType.IsVisible:
                    if (!ParentsVisible(requirement.checkAll, requirement.atLeast)) return false;
                    break;
                case SkillRequirement.RequirementType.IsActive:
                    if (!ParentsActive(requirement.checkAll, requirement.atLeast)) return false;
                    break;
                case SkillRequirement.RequirementType.AtLevel:
                    if (!ParentsAtLevel(requirement.checkAll, requirement.atLeast, requirement.levelMinimum)) return false;
                    break;
                case SkillRequirement.RequirementType.IsCapped:
                    if (!ParentsCapped(requirement.checkAll, requirement.atLeast)) return false;
                    break;
                default:
                    break;
            }
        }
        return true;
    }

    private bool ParentsVisible(bool checkAllParents, int atLeast)
    {
        if (checkAllParents)
        {
            foreach (Skill skill in parentSkills)
            {
                if (skill.state != SkillState.Visible) return false;
            }
        }
        else
        {
            int count = 0;
            foreach (Skill skill in parentSkills)
            {
                if (skill.state == SkillState.Visible) count++;
            }
            if (count < atLeast) return false;
        }
        return true;
    }

    private bool ParentsActive(bool checkAllParents, int atLeast)
    {
        if (checkAllParents)
        {
            foreach (Skill skill in parentSkills)
            {
                if (skill.state != SkillState.Active) return false;
            }
        }
        else
        {
            int count = 0;
            foreach (Skill skill in parentSkills)
            {
                if (skill.state == SkillState.Active) count++;
            }
            if (count < atLeast) return false;
        }
        return true;
    }

    private bool ParentsAtLevel(bool checkAllParents, int atLeast, int levelMinimum)
    {
        if (checkAllParents)
        {
            foreach (Skill skill in parentSkills)
            {
                if (skill.currentLevel < levelMinimum) return false;
            }
        }
        else
        {
            int count = 0;
            foreach (Skill skill in parentSkills)
            {
                if (skill.currentLevel >= levelMinimum) count++;
            }
            if (count < atLeast) return false;
        }
        return true;
    }

    private bool ParentsCapped(bool checkAllParents, int atLeast)
    {
        if (checkAllParents)
        {
            foreach (Skill skill in parentSkills)
            {
                if (skill.capped == false) return false;
            }
        }
        else
        {
            int count = 0;
            foreach (Skill skill in parentSkills)
            {
                if (skill.capped == true) count++;
            }
            if (count < atLeast) return false;
        }
        return true;
    }
}

[System.Serializable]
public class SkillLevel
{
    public SkillTree.CurrencyTypes currencyType;
    public int cost;
    public UnityEvent OnLevelBought;
}

[System.Serializable]
public class SkillRequirement
{
    public enum RequirementType
    {
        IsVisible,
        IsActive,
        AtLevel,
        IsCapped
    }

    public RequirementType requirementType;

    public bool checkAll;
    public int atLeast;
    public int levelMinimum;
}