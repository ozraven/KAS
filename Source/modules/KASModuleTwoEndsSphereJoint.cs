﻿// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv1;
using KSPDev.KSPInterfaces;
using KSPDev.LogUtils;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace KAS {

/// <summary>Module that offers a highly configurable setup of three PhysX joints.</summary>
/// <remarks>
/// One spherical joint is located at the source part, another spherical joint is located at the
/// target part. The joints are connected with a third joint that is setup as prismatic. Such setup
/// allows source and target parts rotationg relative to each other. Distance between the parts is
/// limited by the prismatic joint.
/// <para>
/// By default end spherical joints don't allow rotation around main axis. This degree of freedom is
/// satisfied by the primsatic joint which allows such rotation. Defaults can be overridden in the
/// children classes.
/// </para>
/// </remarks>
/// <seealso cref="KASModuleJointBase.CreateJoint"/>
/// <seealso href="http://docs.nvidia.com/gameworks/content/gameworkslibrary/physx/guide/Manual/Joints.html#spherical-joint">
/// PhysX: Spherical joint</seealso>
/// <seealso href="http://docs.nvidia.com/gameworks/content/gameworkslibrary/physx/guide/Manual/Joints.html#prismatic-joint">
/// PhysX: Prismatic joint</seealso>
// TODO(ihsoft): Add an image.
public class KASModuleTwoEndsSphereJoint : KASModuleJointBase,
    // KSP interfaces.
    IJointLockState,
    // KAS interfaces.
    IKasJointEventsListener,
    // KSPDev syntax sugar interfaces.
    IPartModule, IsDestroyable, IKSPDevJointLockState {

  #region Part's config fields
  /// <summary>Spring force of the prismatic joint that limits the distance.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public float strutSpringForce = Mathf.Infinity;

  /// <summary>Damper force of the spring that limits the distance.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public float strutSpringDamperRatio = 0.1f;  // 10% of the force.

  /// <summary>Tells if joined parts can move relative to each other.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public bool isUnlockedJoint;
  #endregion

  #region Inheritable properties
  /// <summary>Source sphere joint.</summary>
  /// <value>PhysX joint at the source part. <c>null</c> if there is no joint established.</value>
  /// <remarks>It doesn't allow linear movements but does allow rotation around any axis.</remarks>
  /// <seealso cref="KASModuleJointBase.cfgSourceLinkAngleLimit"/>.
  protected ConfigurableJoint srcJoint { get; private set; }

  /// <summary>Target sphere joint.</summary>
  /// <value>PhysX joint at the target part. <c>null</c> if there is no joint established.</value>
  /// <remarks>It doesn't allow linear movements but does allow rotation around any axis.</remarks>
  /// <seealso cref="KASModuleJointBase.cfgTargetLinkAngleLimit"/>
  protected ConfigurableJoint trgJoint { get; private set; }

  /// <summary>Joint that ties two sphere joints together.</summary>
  /// <value>
  /// PhysX joint that connects the source and the end rigid objects. <c>null</c> if there is no
  /// joint established.
  /// </value>
  /// <remarks>
  /// It doesn't allow rotations but does allow linear movements. Rotations and shrink/stretch
  /// limits are set via config settings.
  /// </remarks>
  /// <seealso cref="strutSpringForce"/>
  /// <seealso cref="KASModuleJointBase.cfgMinLinkLength"/>
  /// <seealso cref="KASModuleJointBase.cfgMaxLinkLength"/>
  protected ConfigurableJoint strutJoint { get; private set; }
  #endregion

  #region PartModule overrides
  /// <inheritdoc/>
  public override void OnAwake() {
    base.OnAwake();
    GameEvents.onProtoPartSnapshotSave.Add(OnProtoPartSnapshotSave);
  }

  /// <inheritdoc/>
  public override void OnSave(ConfigNode node) {
    base.OnSave(node);
    if (isLinked) {
      // Note that part iteslf has already been saved into the config with the incorrect data. This
      // data will be fixed in onProtoPartSnapshotSave.
      vessel.parts.ForEach(x => x.UpdateOrgPosAndRot(vessel.rootPart));
    }
  }
  #endregion

  #region IsDestroyable implementation
  /// <inheritdoc/>
  public virtual void OnDestroy() {
    GameEvents.onProtoPartSnapshotSave.Remove(OnProtoPartSnapshotSave);
  }
  #endregion

  #region IJointLockState implemenation
  /// <inheritdoc/>
  public bool IsJointUnlocked() {
    return isUnlockedJoint;
  }
  #endregion

  #region ILinkJoint overrides
  /// <inheritdoc/>
  protected override void AttachParts() {
    // Explicitly not calling the base since it would create a rigid joint!
    SetupJoints();
  }

  /// <inheritdoc/>
  protected override void CoupleParts() {
    base.CoupleParts();
    if (isLinked) {
      // The stock joint is rigid, drop it.
      if (partJoint != null) {
        HostedDebugLog.Fine(this, "Dropping the stock joint on: {0}", partJoint.Child);
        partJoint.DestroyJoint();
        partJoint.Child.attachJoint = null;
      }
      SetupJoints();
    }
  }
  #endregion

  void SetupJoints() {
    HostedDebugLog.Fine(this, "Creating a 3-joints assembly");
    // Create end spherical joints.
    srcJoint = CreateJointEnd(
      linkSource.nodeTransform, linkSource.part.rb, "KASJointSrc", sourceLinkAngleLimit);
    trgJoint = CreateJointEnd(
      linkTarget.nodeTransform, linkTarget.part.rb, "KASJointTrg", targetLinkAngleLimit);
    srcJoint.transform.LookAt(trgJoint.transform, linkSource.nodeTransform.up);
    trgJoint.transform.LookAt(srcJoint.transform, linkTarget.nodeTransform.up);

    // Link end joints with a prismatic joint.
    strutJoint = srcJoint.gameObject.AddComponent<ConfigurableJoint>();
    KASAPI.JointUtils.ResetJoint(strutJoint);
    KASAPI.JointUtils.SetupPrismaticJoint(
        strutJoint, springForce: strutSpringForce, springDamperRatio: strutSpringDamperRatio);
    // Main axis (Z in the game coordinates) must be allowed for rotation to allow arbitrary end
    // joints rotations.
    strutJoint.angularXMotion = ConfigurableJointMotion.Free;
    strutJoint.connectedBody = trgJoint.GetComponent<Rigidbody>();
    strutJoint.enablePreprocessing = true;
    SetBreakForces(strutJoint, linkBreakForce, Mathf.Infinity);

    customJoints = new List<ConfigurableJoint>();
    customJoints.Add(srcJoint);
    customJoints.Add(trgJoint);
    customJoints.Add(strutJoint);
  }

  #region IKasJointEventsListener implementation
  /// <inheritdoc/>
  public virtual void OnKASJointBreak(GameObject hostObj, float breakForce) {
    // Check for the linked state since there can be multiple joints destroyed in the same frame.
    if (isLinked) {
      linkSource.BreakCurrentLink(LinkActorType.Physics);
    }
  }
  #endregion

  #region Inheritable static methods
  /// <summary>Sets up a rigidbody so that it has little or none physics effect.</summary>
  /// <param name="targetRb">The rigidbody to adjust.</param>
  /// <param name="refRb">The rigidbody to get copy physics from.</param>
  protected static void SetupNegligibleRb(Rigidbody targetRb, Rigidbody refRb) {
    targetRb.mass = 0.001f;
    targetRb.useGravity = false;
    targetRb.velocity = refRb.velocity;
    targetRb.angularVelocity = refRb.angularVelocity;
  }
  #endregion

  #region Private utility methods
  /// <summary>
  /// Creates a game object joined with the attach node via a spherical joint. The joint is locked
  /// for rotation around main axis (Z).
  /// </summary>
  /// <remarks>
  /// Joint object will be aligned exactly to the attach node. This will result in zero anchor nodes
  /// and zero/identity relative position and rotation. Caller needs to adjust position/rotation of
  /// the created object as needed, but rotation around Z axis must not be touched since it's
  /// locked.
  /// <para>
  /// Joint object will have rigidobject created. Its physical settings will be default. Caller may
  /// need to adjust the properties.
  /// </para>
  /// </remarks>
  /// <param name="nodeTransform">The tranform to orient new joint to.</param>
  /// <param name="targetRb">The rigid body to attach the joint to.</param>
  /// <param name="objName">The name of the game object for the joint.</param>
  /// <param name="angleLimit">The degree of freedom of the joint.</param>
  /// <returns>Joint object.</returns>
  ConfigurableJoint CreateJointEnd(
      Transform nodeTransform, Rigidbody targetRb, string objName, float angleLimit) {
    if (targetRb == null) {
      throw new InvalidOperationException(string.Format(
          "Cannot create a joint to {0} since it doesn't have rigidbody (physicsless?)",
          nodeTransform));
    }
    var jointObj = new GameObject(objName);
    jointObj.transform.position = nodeTransform.position;
    jointObj.transform.rotation = nodeTransform.rotation;
    jointObj.AddComponent<BrokenJointListener>().hostPart = part;
    SetupNegligibleRb(jointObj.AddComponent<Rigidbody>(), targetRb);
    var joint = jointObj.AddComponent<ConfigurableJoint>();
    KASAPI.JointUtils.ResetJoint(joint);
    KASAPI.JointUtils.SetupSphericalJoint(joint, angleLimit: angleLimit);
    joint.enablePreprocessing = true;
    joint.connectedBody = targetRb;
    SetBreakForces(joint, linkBreakForce, linkBreakTorque);
    return joint;
  }

  /// <summary>
  /// Fixes the stored org position and rotation since they are saved before UpdateOrgPosAndRot
  /// happens.
  /// </summary>
  void OnProtoPartSnapshotSave(GameEvents.FromToAction<ProtoPartSnapshot, ConfigNode> action) {
    if (isUnlockedJoint && isLinked && action.to != null && action.from.partRef == part) {
      var node = action.to;
      node.SetValue("position", part.orgPos);
      node.SetValue("rotation", part.orgRot);
    }
  }
  #endregion
}

}  // namespace
