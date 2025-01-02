#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(Skill))]
public class SkillEditor : Editor
{
    private bool showLevels = true;
    private bool showAssociatedSkills = true;
    private bool showRequirements = true;

    public override void OnInspectorGUI()
    {
        Skill skill = (Skill)target;

        EditorGUI.BeginChangeCheck();
        skill.title = EditorGUILayout.TextField("Title", skill.title);
        if (EditorGUI.EndChangeCheck())
        {
            skill.gameObject.name = "(Skill) " + skill.title;
        }

        EditorGUILayout.Space();
        skill.state = (Skill.SkillState)EditorGUILayout.EnumPopup("State", skill.state);

        EditorGUILayout.Space();
        showLevels = EditorGUILayout.Foldout(showLevels, "Level Options", true, EditorStyles.boldLabel);
        if (showLevels)
        {
            skill.currentLevel = EditorGUILayout.IntField("Current Level", skill.currentLevel);
            skill.capped = EditorGUILayout.Toggle("Capped", skill.capped);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Levels", EditorStyles.boldLabel);
            DrawLevels(skill.levels);
        }

        EditorGUILayout.Space();
        showAssociatedSkills = EditorGUILayout.Foldout(showAssociatedSkills, "Associated Skills", true, EditorStyles.boldLabel);
        if (showAssociatedSkills)
        {

            EditorGUILayout.LabelField("Parents", EditorStyles.boldLabel);
            DrawSkillList(skill.parentSkills);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Childs", EditorStyles.boldLabel);
            DrawSkillList(skill.childSkills);
        }

        EditorGUILayout.Space();
        if (skill.parentSkills.Count != 0)
        {
            showRequirements = EditorGUILayout.Foldout(showRequirements, "Requirements", true, EditorStyles.boldLabel);
            if (showRequirements)
            {
                EditorGUILayout.LabelField("Visibility Requirements", EditorStyles.boldLabel);
                DrawRequirements(skill.visibilityRequirements, skill.parentSkills);

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Unlockable Requirements", EditorStyles.boldLabel);
                DrawRequirements(skill.unlockableRequirements, skill.parentSkills);
            }
        }

        if (GUI.changed)
        {
            EditorUtility.SetDirty(skill);
        }
    }

    private void DrawLevels(List<SkillLevel> levels)
    {
        if (levels == null) return;

        for (int i = 0; i < levels.Count; i++)
        {
            SkillLevel level = levels[i];

            EditorGUILayout.BeginVertical("box");

            level.currencyType = (SkillTree.CurrencyTypes)EditorGUILayout.EnumPopup("Currency Type", level.currencyType);
            level.cost = EditorGUILayout.IntField("Cost", level.cost);

            EditorGUILayout.LabelField("On Level Bought");
            EditorGUILayout.PropertyField(new SerializedObject(target).FindProperty("levels").GetArrayElementAtIndex(i).FindPropertyRelative("OnLevelBought"));

            if (GUILayout.Button("Remove Level"))
            {
                levels.RemoveAt(i);
                break;
            }

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("Add Level"))
        {
            levels.Add(new SkillLevel());
        }
    }

    private void DrawSkillList(List<Skill> skillList)
    {
        if (skillList == null) return;

        for (int i = 0; i < skillList.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();

            Skill skill = skillList[i];
            if (skill != null)
            {
                EditorGUILayout.LabelField(skill.title, GUILayout.MaxWidth(200));
            }
            else
            {
                EditorGUILayout.LabelField("(None)", GUILayout.MaxWidth(200));
            }

            skillList[i] = (Skill)EditorGUILayout.ObjectField(skillList[i], typeof(Skill), true);

            if (GUILayout.Button("-", GUILayout.MaxWidth(25)))
            {
                skillList.RemoveAt(i);
                break;
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("Add Skill"))
        {
            skillList.Add(null);
        }
    }

    private void DrawRequirements(List<SkillRequirement> requirements, List<Skill> parentSkills)
    {
        if (requirements == null) return;

        for (int i = 0; i < requirements.Count; i++)
        {
            SkillRequirement requirement = requirements[i];

            EditorGUILayout.BeginVertical("box");

            requirement.requirementType = (SkillRequirement.RequirementType)EditorGUILayout.EnumPopup("Requirement Type", requirement.requirementType);

            requirement.checkAll = EditorGUILayout.Toggle("Check All Parents", requirement.checkAll);

            if (!requirement.checkAll)
            {
                int maxAtLeast = Mathf.Max(1, parentSkills.Count - 1);
                if (maxAtLeast == 1)
                {
                    requirement.atLeast = 1;
                    EditorGUILayout.LabelField("At Least: 1 (fixed)");
                }
                else
                {
                    requirement.atLeast = EditorGUILayout.IntSlider("At Least", requirement.atLeast, 1, maxAtLeast);
                }
            }

            if (requirement.requirementType == SkillRequirement.RequirementType.AtLevel)
            {
                int minLevel = 1;
                int maxLevel = 1;

                if (parentSkills != null && parentSkills.Count > 0)
                {
                    maxLevel = Mathf.Max(1, GetMinimumParentLevels(parentSkills));
                }

                if (minLevel == maxLevel)
                {
                    requirement.levelMinimum = minLevel;
                    EditorGUILayout.LabelField($"Level Minimum: {minLevel} (fixed)");
                }
                else
                {
                    requirement.levelMinimum = EditorGUILayout.IntSlider("Level Minimum", requirement.levelMinimum, minLevel, maxLevel);
                }
            }

            if (GUILayout.Button("Remove Requirement"))
            {
                requirements.RemoveAt(i);
                break;
            }

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("Add Requirement"))
        {
            requirements.Add(new SkillRequirement());
        }
    }

    private int GetMinimumParentLevels(List<Skill> parentSkills)
    {
        int minLevels = int.MaxValue;
        foreach (Skill parent in parentSkills)
        {
            if (parent.levels != null && parent.levels.Count > 0)
            {
                minLevels = Mathf.Min(minLevels, parent.levels.Count);
            }
        }

        return minLevels == int.MaxValue ? 1 : minLevels;
    }
}
#endif