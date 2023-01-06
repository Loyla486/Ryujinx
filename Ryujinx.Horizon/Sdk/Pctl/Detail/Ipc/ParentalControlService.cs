using Ryujinx.Common.Logging;
using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Pctl;
using Ryujinx.Horizon.Sdk.Pctl.Detail.Service.Watcher;
using Ryujinx.Horizon.Sdk.Sf;
using Ryujinx.Horizon.Sdk.Sf.Hipc;
using Ryujinx.Horizon.Sdk.Time;
using System;
using ApplicationId = Ryujinx.Horizon.Sdk.Ncm.ApplicationId;

namespace Ryujinx.Horizon.Sdk.Pctl.Detail.Ipc
{
    partial class ParentalControlService : IParentalControlService
    {
        private ulong                    _pid;
        private int                      _permissionFlag;
        private ulong                    _titleId;
        private ParentalControlFlagValue _parentalControlFlag;
        private int[]                    _ratingAge;

        // TODO: Find where they are set.
        private bool _restrictionEnabled                  = false;
        private bool _featuresRestriction                 = false;
        private bool _freeCommunicationEnabled            = false;
        private bool _stereoVisionRestrictionConfigurable = true;
        private bool _stereoVisionRestriction             = false;

        [CmifCommand(1)] // 4.0.0+
        // Initialize()
        public Result Initialize()
        {
            if ((_permissionFlag & 0x8001) == 0)
            {
                return PctlResult.PermissionDenied;
            }

            Result result = PctlResult.InvalidPid;

            if (_pid != 0)
            {
                if ((_permissionFlag & 0x40) == 0)
                {
                    /*ulong titleId = ApplicationLaunchProperty.GetByPid(context).TitleId;

                    if (titleId != 0)
                    {
                        _titleId = titleId;

                        // TODO: Call nn::arp::GetApplicationControlProperty here when implemented, if it return ResultCode.Success we assign fields.
                        _ratingAge           = Array.ConvertAll(context.Device.Application.ControlData.Value.RatingAge.ItemsRo.ToArray(), Convert.ToInt32);
                        _parentalControlFlag = context.Device.Application.ControlData.Value.ParentalControlFlag;
                    }*/
                }

                if (_titleId != 0)
                {
                    // TODO: Service store some private fields in another static object.

                    if ((_permissionFlag & 0x8040) == 0)
                    {
                        // TODO: Service store TitleId and FreeCommunicationEnabled in another static object.
                        //       When it's done it signal an event in this static object.
                        Logger.Stub?.PrintStub(LogClass.ServicePctl);
                    }
                }

                result = Result.Success;
            }

            return result;
        }

        [CmifCommand(1001)]
        // CheckFreeCommunicationPermission()
        public Result CheckFreeCommunicationPermission()
        {
            if (_parentalControlFlag == ParentalControlFlagValue.FreeCommunication && _restrictionEnabled)
            {
                // TODO: It seems to checks if an entry exists in the FreeCommunicationApplicationList using the TitleId.
                //       Then it returns FreeCommunicationDisabled if the entry doesn't exist.

                return PctlResult.FreeCommunicationDisabled;
            }

            _freeCommunicationEnabled = true;

            Logger.Stub?.PrintStub(LogClass.ServicePctl);

            return Result.Success;
        }

        [CmifCommand(1002)]
        public Result ConfirmLaunchApplicationPermission(ApplicationId arg0, [Buffer(HipcBufferFlags.In | HipcBufferFlags.Pointer)] ReadOnlySpan<sbyte> arg1, bool arg2)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1003)]
        public Result ConfirmResumeApplicationPermission(ApplicationId arg0, [Buffer(HipcBufferFlags.In | HipcBufferFlags.Pointer)] ReadOnlySpan<sbyte> arg1, bool arg2)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1004)]
        public Result ConfirmSnsPostPermission()
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1005)]
        public Result ConfirmSystemSettingsPermission()
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1006)]
        public Result IsRestrictionTemporaryUnlocked(out bool arg0)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                arg0 = default;
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1007)]
        public Result RevertRestrictionTemporaryUnlocked()
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1008)]
        public Result EnterRestrictedSystemSettings()
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1009)]
        public Result LeaveRestrictedSystemSettings()
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1010)]
        public Result IsRestrictedSystemSettingsEntered(out bool arg0)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                arg0 = default;
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1011)]
        public Result RevertRestrictedSystemSettingsEntered()
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1012)]
        public Result GetRestrictedFeatures(out int arg0)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                arg0 = default;
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1013)] // 4.0.0+
        // ConfirmStereoVisionPermission()
        public Result ConfirmStereoVisionPermission()
        {
            return IsStereoVisionPermittedImpl();
        }

        [CmifCommand(1014)]
        public Result ConfirmPlayableApplicationVideoOld([Buffer(HipcBufferFlags.In | HipcBufferFlags.Pointer)] ReadOnlySpan<sbyte> arg0)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1015)]
        public Result ConfirmPlayableApplicationVideo(ApplicationId arg0, [Buffer(HipcBufferFlags.In | HipcBufferFlags.Pointer)] ReadOnlySpan<sbyte> arg1)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1016)]
        public Result ConfirmShowNewsPermission([Buffer(HipcBufferFlags.In | HipcBufferFlags.Pointer)] ReadOnlySpan<sbyte> arg0)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1017)] // 10.0.0+
        // EndFreeCommunication()
        public Result EndFreeCommunication()
        {
            _freeCommunicationEnabled = false;

            return Result.Success;
        }

        [CmifCommand(1018)]
        // IsFreeCommunicationAvailable()
        public Result IsFreeCommunicationAvailable()
        {
            if (_parentalControlFlag == ParentalControlFlagValue.FreeCommunication && _restrictionEnabled)
            {
                // TODO: It seems to checks if an entry exists in the FreeCommunicationApplicationList using the TitleId.
                //       Then it returns FreeCommunicationDisabled if the entry doesn't exist.

                return PctlResult.FreeCommunicationDisabled;
            }

            Logger.Stub?.PrintStub(LogClass.ServicePctl);

            return Result.Success;
        }

        [CmifCommand(1031)]
        // IsRestrictionEnabled() -> b8
        public Result IsRestrictionEnabled(out bool restrictionEnabled)
        {
            if ((_permissionFlag & 0x140) == 0)
            {
                restrictionEnabled = default;
                return PctlResult.PermissionDenied;
            }

            restrictionEnabled = _restrictionEnabled;

            return Result.Success;
        }

        [CmifCommand(1032)]
        public Result GetSafetyLevel(out int arg0)
        {
            arg0 = default;

            return Result.Success;
        }

        [CmifCommand(1033)]
        public Result SetSafetyLevel(int arg0)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1034)]
        public Result GetSafetyLevelSettings(out RestrictionSettings arg0, int arg1)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                arg0 = default;
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1035)]
        public Result GetCurrentSettings(out RestrictionSettings arg0)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                arg0 = default;
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1036)]
        public Result SetCustomSafetyLevelSettings(RestrictionSettings arg0)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1037)]
        public Result GetDefaultRatingOrganization(out int arg0)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                arg0 = default;
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1038)]
        public Result SetDefaultRatingOrganization(int arg0)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1039)]
        public Result GetFreeCommunicationApplicationListCount(out int arg0)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                arg0 = default;
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1042)]
        public Result AddToFreeCommunicationApplicationList(ApplicationId arg0)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1043)]
        public Result DeleteSettings()
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1044)]
        public Result GetFreeCommunicationApplicationList(out int arg0, [Buffer(HipcBufferFlags.Out | HipcBufferFlags.MapAlias)] Span<FreeCommunicationApplicationInfo> arg1, int arg2)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                arg0 = default;
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1045)]
        public Result UpdateFreeCommunicationApplicationList([Buffer(HipcBufferFlags.In | HipcBufferFlags.MapAlias)] ReadOnlySpan<FreeCommunicationApplicationInfo> arg0)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1046)]
        public Result DisableFeaturesForReset()
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1047)]
        public Result NotifyApplicationDownloadStarted(ApplicationId arg0)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1048)]
        public Result NotifyNetworkProfileCreated()
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1049)]
        public Result ResetFreeCommunicationApplicationList()
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1061)] // 4.0.0+
        // ConfirmStereoVisionRestrictionConfigurable()
        public Result ConfirmStereoVisionRestrictionConfigurable()
        {
            if ((_permissionFlag & 2) == 0)
            {
                return PctlResult.PermissionDenied;
            }

            if (_stereoVisionRestrictionConfigurable)
            {
                return Result.Success;
            }
            else
            {
                return PctlResult.StereoVisionRestrictionConfigurableDisabled;
            }
        }

        [CmifCommand(1062)] // 4.0.0+
        // GetStereoVisionRestriction() -> bool
        public Result GetStereoVisionRestriction(out bool stereoVisionRestriction)
        {
            if ((_permissionFlag & 0x200) == 0)
            {
                stereoVisionRestriction = default;
                return PctlResult.PermissionDenied;
            }

            stereoVisionRestriction = false;

            if (_stereoVisionRestrictionConfigurable)
            {
                stereoVisionRestriction = _stereoVisionRestriction;
            }

            return Result.Success;
        }

        [CmifCommand(1063)] // 4.0.0+
        // SetStereoVisionRestriction(bool)
        public Result SetStereoVisionRestriction(bool stereoVisionRestriction)
        {
            if ((_permissionFlag & 0x200) == 0)
            {
                return PctlResult.PermissionDenied;
            }

            if (!_featuresRestriction)
            {
                if (_stereoVisionRestrictionConfigurable)
                {
                    _stereoVisionRestriction = stereoVisionRestriction;

                    // TODO: It signals an internal event of service. We have to determine where this event is used.
                }
            }

            return Result.Success;
        }

        [CmifCommand(1064)] // 5.0.0+
        // ResetConfirmedStereoVisionPermission()
        public Result ResetConfirmedStereoVisionPermission()
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1065)] // 5.0.0+
        // IsStereoVisionPermitted() -> bool
        public Result IsStereoVisionPermitted(out bool isStereoVisionPermitted)
        {
            isStereoVisionPermitted = false;

            Result result = IsStereoVisionPermittedImpl();

            if (result == Result.Success)
            {
                isStereoVisionPermitted = true;
            }

            return result;
        }

        private Result IsStereoVisionPermittedImpl()
        {
            /*
                // TODO: Application Exemptions are read from file "appExemptions.dat" in the service savedata.
                //       Since we don't support the pctl savedata for now, this can be implemented later.

                if (appExemption)
                {
                    return Result.Success;
                }
            */

            if (_stereoVisionRestrictionConfigurable && _stereoVisionRestriction)
            {
                return PctlResult.StereoVisionDenied;
            }
            else
            {
                return Result.Success;
            }
        }

        [CmifCommand(1201)]
        public Result UnlockRestrictionTemporarily([Buffer(HipcBufferFlags.In | HipcBufferFlags.Pointer)] ReadOnlySpan<sbyte> arg0)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1202)]
        public Result UnlockSystemSettingsRestriction([Buffer(HipcBufferFlags.In | HipcBufferFlags.Pointer)] ReadOnlySpan<sbyte> arg0)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1203)]
        public Result SetPinCode([Buffer(HipcBufferFlags.In | HipcBufferFlags.Pointer)] ReadOnlySpan<sbyte> arg0)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1204)]
        public Result GenerateInquiryCode(out InquiryCode arg0)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                arg0 = default;
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1205)]
        public Result CheckMasterKey(out bool arg0, InquiryCode arg1, [Buffer(HipcBufferFlags.In | HipcBufferFlags.Pointer)] ReadOnlySpan<sbyte> arg2)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                arg0 = default;
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1206)]
        public Result GetPinCodeLength(out int arg0)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                arg0 = default;
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1207)]
        public Result GetPinCodeChangedEvent([CopyHandle] out int arg0)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                arg0 = default;
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1208)]
        public Result GetPinCode(out int arg0, [Buffer(HipcBufferFlags.Out | HipcBufferFlags.Pointer)] Span<sbyte> arg1)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                arg0 = default;
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1403)]
        public Result IsPairingActive(out bool arg0)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                arg0 = default;
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1406)]
        public Result GetSettingsLastUpdated(out PosixTime arg0)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                arg0 = default;
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1411)]
        public Result GetPairingAccountInfo(out PairingAccountInfoBase arg0, PairingInfoBase arg1)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                arg0 = default;
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1421)]
        public Result GetAccountNickname(out uint arg0, [Buffer(HipcBufferFlags.Out | HipcBufferFlags.Pointer)] Span<sbyte> arg1, PairingAccountInfoBase arg2)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                arg0 = default;
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1424)]
        public Result GetAccountState(out int arg0, PairingAccountInfoBase arg1)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                arg0 = default;
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1425)]
        public Result RequestPostEvents(out int arg0, [Buffer(HipcBufferFlags.Out | HipcBufferFlags.MapAlias)] Span<EventData> arg1)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                arg0 = default;
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1426)]
        public Result GetPostEventInterval(out int arg0)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                arg0 = default;
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1427)]
        public Result SetPostEventInterval(int arg0)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1432)]
        public Result GetSynchronizationEvent([CopyHandle] out int arg0)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                arg0 = default;
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1451)]
        public Result StartPlayTimer()
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1452)]
        public Result StopPlayTimer()
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1453)]
        public Result IsPlayTimerEnabled(out bool arg0)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                arg0 = default;
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1454)]
        public Result GetPlayTimerRemainingTime(out TimeSpanType arg0)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                arg0 = default;
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1455)]
        public Result IsRestrictedByPlayTimer(out bool arg0)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                arg0 = default;
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1456)]
        public Result GetPlayTimerSettings(out PlayTimerSettings arg0)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                arg0 = default;
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1457)]
        public Result GetPlayTimerEventToRequestSuspension([CopyHandle] out int arg0)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                arg0 = default;
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1458)]
        public Result IsPlayTimerAlarmDisabled(out bool arg0)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                arg0 = default;
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1471)]
        public Result NotifyWrongPinCodeInputManyTimes()
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1472)]
        public Result CancelNetworkRequest()
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1473)]
        public Result GetUnlinkedEvent([CopyHandle] out int arg0)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                arg0 = default;
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1474)]
        public Result ClearUnlinkedEvent()
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1601)]
        public Result DisableAllFeatures(out bool arg0)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                arg0 = default;
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1602)]
        public Result PostEnableAllFeatures(out bool arg0)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                arg0 = default;
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1603)]
        public Result IsAllFeaturesDisabled(out bool arg0, out bool arg1)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                arg0 = default;
                arg1 = default;
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1901)]
        public Result DeleteFromFreeCommunicationApplicationListForDebug(ApplicationId arg0)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1902)]
        public Result ClearFreeCommunicationApplicationListForDebug()
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1903)]
        public Result GetExemptApplicationListCountForDebug(out int arg0)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                arg0 = default;
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1904)]
        public Result GetExemptApplicationListForDebug(out int arg0, [Buffer(HipcBufferFlags.Out | HipcBufferFlags.MapAlias)] Span<ExemptApplicationInfo> arg1, int arg2)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                arg0 = default;
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1905)]
        public Result UpdateExemptApplicationListForDebug([Buffer(HipcBufferFlags.In | HipcBufferFlags.MapAlias)] ReadOnlySpan<ExemptApplicationInfo> arg0)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1906)]
        public Result AddToExemptApplicationListForDebug(ApplicationId arg0)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1907)]
        public Result DeleteFromExemptApplicationListForDebug(ApplicationId arg0)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1908)]
        public Result ClearExemptApplicationListForDebug()
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1941)]
        public Result DeletePairing()
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1951)]
        public Result SetPlayTimerSettingsForDebug(PlayTimerSettings arg0)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1952)]
        public Result GetPlayTimerSpentTimeForTest(out TimeSpanType arg0)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                arg0 = default;
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(1953)]
        public Result SetPlayTimerAlarmDisabledForDebug(bool arg0)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(2001)]
        public Result RequestPairingAsync(out AsyncData arg0, [CopyHandle] out int arg1, [Buffer(HipcBufferFlags.In | HipcBufferFlags.Pointer)] ReadOnlySpan<sbyte> arg2)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                arg0 = default;
                arg1 = default;
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(2002)]
        public Result FinishRequestPairing(out PairingInfoBase arg0, AsyncData arg1)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                arg0 = default;
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(2003)]
        public Result AuthorizePairingAsync(out AsyncData arg0, [CopyHandle] out int arg1, PairingInfoBase arg2)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                arg0 = default;
                arg1 = default;
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(2004)]
        public Result FinishAuthorizePairing(out PairingInfoBase arg0, AsyncData arg1)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                arg0 = default;
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(2005)]
        public Result RetrievePairingInfoAsync(out AsyncData arg0, [CopyHandle] out int arg1)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                arg0 = default;
                arg1 = default;
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(2006)]
        public Result FinishRetrievePairingInfo(out PairingInfoBase arg0, AsyncData arg1)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                arg0 = default;
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(2007)]
        public Result UnlinkPairingAsync(out AsyncData arg0, [CopyHandle] out int arg1, bool arg2)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                arg0 = default;
                arg1 = default;
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(2008)]
        public Result FinishUnlinkPairing(AsyncData arg0, bool arg1)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(2009)]
        public Result GetAccountMiiImageAsync(out AsyncData arg0, [CopyHandle] out int arg1, out uint arg2, [Buffer(HipcBufferFlags.Out | HipcBufferFlags.MapAlias)] Span<byte> arg3, PairingAccountInfoBase arg4)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                arg0 = default;
                arg1 = default;
                arg2 = default;
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(2010)]
        public Result FinishGetAccountMiiImage(out uint arg0, [Buffer(HipcBufferFlags.Out | HipcBufferFlags.MapAlias)] Span<byte> arg1, AsyncData arg2)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                arg0 = default;
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(2011)]
        public Result GetAccountMiiImageContentTypeAsync(out AsyncData arg0, [CopyHandle] out int arg1, out uint arg2, [Buffer(HipcBufferFlags.Out | HipcBufferFlags.Pointer)] Span<sbyte> arg3, PairingAccountInfoBase arg4)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                arg0 = default;
                arg1 = default;
                arg2 = default;
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(2012)]
        public Result FinishGetAccountMiiImageContentType(out uint arg0, [Buffer(HipcBufferFlags.Out | HipcBufferFlags.Pointer)] Span<sbyte> arg1, AsyncData arg2)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                arg0 = default;
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(2013)]
        public Result SynchronizeParentalControlSettingsAsync(out AsyncData arg0, [CopyHandle] out int arg1)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                arg0 = default;
                arg1 = default;
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(2014)]
        public Result FinishSynchronizeParentalControlSettings(AsyncData arg0)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(2015)]
        public Result FinishSynchronizeParentalControlSettingsWithLastUpdated(out PosixTime arg0, AsyncData arg1)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                arg0 = default;
                return Result.Success;
            }

            throw new NotImplementedException();
        }

        [CmifCommand(2016)]
        public Result RequestUpdateExemptionListAsync(out AsyncData arg0, [CopyHandle] out int arg1, ApplicationId arg2, bool arg3)
        {
            if (HorizonStatic.Options.IgnoreMissingServices)
            {
                arg0 = default;
                arg1 = default;
                return Result.Success;
            }

            throw new NotImplementedException();
        }
    }
}