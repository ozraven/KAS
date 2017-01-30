﻿// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Linq;
using UnityEngine;
using KSPDev.ModelUtils;

namespace KAS {

/// <summary>Base module for a procedural part that simulates a flexible joint.</summary>
/// <remarks>
/// Use <see cref="CreateStrutJointModel"/> to create joint ends, then orient them so what they look
/// connected at the pivot axile (<see cref="PivotAxileObjName"/>).
/// </remarks>
public abstract class AbstractJointPart : AbstractProceduralModel {
  //FIXME drop attach node fields, move to descendats, and rename
  #region Part's config fields
  /// <summary>Config setting. Texture to use for procedural joint model meshes.</summary>
  /// <remarks>
  /// <para>
  /// This is a <see cref="KSPField"/> annotated field. It's handled by the KSP core and must
  /// <i>not</i> be altered directly. Moreover, in spite of it's declared <c>public</c> it must not
  /// be accessed outside of the module.
  /// </para>
  /// </remarks>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
  /// KSP: KSPField</seealso>
  [KSPField]
  public string jointTexturePath = "";
  /// <summary>
  /// Config setting. Position of the root transform of the procedural joint model.
  /// </summary>
  /// <remarks>
  /// <para>
  /// This is a <see cref="KSPField"/> annotated field. It's handled by the KSP core and must
  /// <i>not</i> be altered directly. Moreover, in spite of it's declared <c>public</c> it must not
  /// be accessed outside of the module.
  /// </para>
  /// </remarks>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
  /// KSP: KSPField</seealso>
  [KSPField]
  public Vector3 attachNodePosition = Vector3.zero;
  /// <summary>
  /// Config setting. Orientation of the root transform of the procedural joint model.
  /// </summary>
  /// <remarks>
  /// <para>
  /// This is a <see cref="KSPField"/> annotated field. It's handled by the KSP core and must
  /// <i>not</i> be altered directly. Moreover, in spite of it's declared <c>public</c> it must not
  /// be accessed outside of the module.
  /// </para>
  /// </remarks>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
  /// KSP: KSPField</seealso>
  [KSPField]
  public Vector3 attachNodeOrientation = Vector3.up;
  #endregion

  /// <summary>
  /// Name of the transform that is used to conenct two levers to form a complete joint. 
  /// </summary>
  protected const string PivotAxileObjName = "PivotAxile";

  #region Model sizes. Be CAREFUL modifying them!
  // These constants make joint model looking solid. Do NOT change them unless you fully understand
  // what is "joint base", "clutch holder" and "clutch". The values are interconnected, so changing
  // one will likely require adjusting some others.
  const float JointBaseDiameter = 0.10f;
  const float JointBaseHeigth = 0.02f;
  const float ClutchHolderThikness = 0.02f;
  const float ClutchHolderWidth = 0.10f;
  const float ClutchHolderLength = 0.05f + 0.01f;
  const float ClutchThikness = 0.03f;
  const float ClutchAxileDiameter = 0.03f;
  const float ClutchAxleExtent = 0.005f;
  const float ClutchAxileLength = 2 * (ClutchThikness + ClutchAxleExtent);
  #endregion

  /// <summary>Dynamically creates model for a joint lever.</summary>
  /// <remarks>Transfrom where two levers can connect is named <see cref="PivotAxileObjName"/>. To
  /// make a complete joint model align pivot axiles of the levers, and rotate one of the levers 180
  /// degrees around Z axis to match the clutches.
  /// <para>All details of the model get populated with main texure <see cref="jointTexturePath"/>.
  /// </para>
  /// <para>Model won't have any colliders setup. Consider using
  /// <see cref="Colliders.SetSimpleCollider"/> on the newly created model to enable collider.
  /// </para>
  /// </remarks>
  /// <param name="transformName">Trasnfrom name of the new lever. Use different names for the
  /// levers to be able loading them on part model load.</param>
  /// <param name="createAxile">If <c>true</c> then axile model will be created, and it will be the
  /// axile tansfrom. Otherwise, the axile transfrom will be an emopty object. Only one lever in the
  /// connection should have axile model.</param>
  /// <returns>Newly created joint lever model. In order to be visible and accessible on the part
  /// the model must be attached to the part's model transform.</returns>
  protected Transform CreateStrutJointModel(string transformName, bool createAxile = true) {
    var material = CreateMaterial(GetTexture(jointTexturePath));
    var jointTransform = new GameObject(transformName).transform;

    // Socket cap.
    var jointBase = Meshes.CreateBox(
        JointBaseDiameter, JointBaseDiameter, JointBaseHeigth, material, parent: jointTransform);
    jointBase.name = "base";
    jointBase.transform.localPosition = new Vector3(0, 0, JointBaseHeigth / 2);

    // Holding bar for the clutcth.
    var clutchHolder = Meshes.CreateBox(
        ClutchHolderThikness, ClutchHolderWidth, ClutchHolderLength, material,
        parent: jointBase.transform);
    clutchHolder.name = "clutchHolder";
    clutchHolder.transform.localPosition = new Vector3(
        ClutchHolderThikness / 2 + (ClutchThikness - ClutchHolderThikness),
        0,
        (ClutchHolderLength + JointBaseHeigth) / 2);

    // The clutch.
    var clutch = Meshes.CreateCylinder(
        ClutchHolderWidth, ClutchThikness, material, parent: clutchHolder.transform);
    clutch.name = "clutch";
    clutch.transform.localRotation = Quaternion.LookRotation(Vector3.left);
    clutch.transform.localPosition =
        new Vector3(-(ClutchThikness - ClutchHolderThikness) / 2, 0, ClutchHolderLength / 2);

    // Axile inside the clutch to join with the opposite joint clutch.
    var pivotTransform = new GameObject(PivotAxileObjName).transform;
    pivotTransform.parent = jointTransform.transform;
    if (createAxile) {
      var clutchAxile = Meshes.CreateCylinder(
          ClutchAxileDiameter, ClutchAxileLength, material, parent: clutchHolder.transform);
      clutchAxile.name = "axile";
      clutchAxile.transform.localRotation = Quaternion.LookRotation(Vector3.left);
      clutchAxile.transform.localPosition =
          new Vector3(-clutchHolder.transform.localPosition.x, 0, ClutchHolderLength / 2);
      pivotTransform.localPosition =
          pivotTransform.InverseTransformPoint(clutchAxile.transform.position);
    } else {
      pivotTransform.localPosition = pivotTransform.InverseTransformPoint(
          clutchHolder.transform.TransformPoint(
              new Vector3(-clutchHolder.transform.localPosition.x, 0, ClutchHolderLength / 2)));
    }

    return jointTransform;
  }
}

}  // namespace