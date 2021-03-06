/////////////////////////////////////////////////////////////////////
// Copyright (c) Autodesk, Inc. All rights reserved
// Written by Forge Design Automation team for Inventor
//
// Permission to use, copy, modify, and distribute this software in
// object code form for any purpose and without fee is hereby granted,
// provided that the above copyright notice appears in all copies and
// that both that copyright notice and the limited warranty and
// restricted rights notice below appear in all supporting
// documentation.
//
// AUTODESK PROVIDES THIS PROGRAM "AS IS" AND WITH ALL FAULTS.
// AUTODESK SPECIFICALLY DISCLAIMS ANY IMPLIED WARRANTY OF
// MERCHANTABILITY OR FITNESS FOR A PARTICULAR USE.  AUTODESK, INC.
// DOES NOT WARRANT THAT THE OPERATION OF THE PROGRAM WILL BE
// UNINTERRUPTED OR ERROR FREE.
/////////////////////////////////////////////////////////////////////

import {combineReducers} from 'redux';
import projectListReducer, * as list from './projectListReducers';
import {notificationReducer} from './notificationReducer';
import parametersReducer, * as params from './parametersReducer';
import updateParametersReducer, * as updateParams from './updateParametersReducer';
import uiFlagsReducer, * as uiFlags from './uiFlagsReducer';
import profileReducer from './profileReducer';
import bomReducer, * as bom from './bomReducer';

export const mainReducer = combineReducers({
    projectList: projectListReducer,
    notifications: notificationReducer,
    parameters: parametersReducer,
    updateParameters: updateParametersReducer,
    uiFlags: uiFlagsReducer,
    profile: profileReducer,
    bom: bomReducer
});

export const getActiveProject = function(state) {
    return list.getActiveProject(state.projectList);
};

export const getProject = function(id, state) {
    return list.getProject(id, state.projectList);
};

export const getParameters = function(projectId, state) {
    return params.getParameters(projectId, state.parameters);
};

export const getUpdateParameters = function(projectId, state) {
    return updateParams.getParameters(projectId, state.updateParameters);
};

export const parametersEditedMessageVisible = function(state) {
    if (state.uiFlags.parametersEditedMessageClosed === true || state.uiFlags.parametersEditedMessageRejected === true )
        return false;

    const activeProject = getActiveProject(state);
    if (!activeProject)
        return false;

    const parameters = getParameters(activeProject.id, state);
    const updateParameters = getUpdateParameters(activeProject.id, state);

    if (!parameters || !updateParameters)
        return false;

    for (const parameterId in parameters) {
        const parameter = parameters[parameterId];
        const updateParameter = updateParameters.find(updatePar => updatePar.name === parameter.name);
        if (parameter.value !== updateParameter.value) {
            return true;
        }
    }

    return false;
};

export const modalProgressShowing = function(state) {
    return uiFlags.modalProgressShowing(state.uiFlags);
};

export const updateFailedShowing = function(state) {
    return uiFlags.updateFailedShowing(state.uiFlags);
};

export const loginFailedShowing = function(state) {
    return uiFlags.loginFailedShowing(state.uiFlags);
};

export const downloadRfaFailedShowing = function(state) {
    return uiFlags.downloadRfaFailedShowing(state.uiFlags);
};

export const reportUrl = function(state) {
    return uiFlags.reportUrl(state.uiFlags);
};

export const rfaProgressShowing = function(state) {
    return uiFlags.rfaProgressShowing(state.uiFlags);
};

export const rfaDownloadUrl = function(state) {
    return uiFlags.rfaDownloadUrl(state.uiFlags);
};

export const uploadPackageDlgVisible = function(state) {
    return uiFlags.uploadPackageDlgVisible(state.uiFlags);
};

export const uploadProgressShowing = function(state) {
    return uiFlags.uploadProgressShowing(state.uiFlags);
};

export const uploadProgressIsDone = function(state) {
    return uiFlags.uploadProgressIsDone(state.uiFlags);
};

export const uploadPackageData = function(state) {
    return uiFlags.uploadPackageData(state.uiFlags);
};

export const uploadFailedShowing = function(state) {
    return uiFlags.uploadFailedShowing(state.uiFlags);
};

export const getProfile = function (state) {
    return state.profile;
};

export const activeTabIndex = function(state) {
    return uiFlags.activeTabIndex(state.uiFlags);
};

export const projectAlreadyExists = function(state) {
    return uiFlags.projectAlreadyExists(state.uiFlags);
};

export const deleteProjectDlgVisible = function(state) {
    return uiFlags.deleteProjectDlgVisible(state.uiFlags);
};

export const checkedProjects = function(state) {
    return uiFlags.checkedProjects(state.uiFlags);
};

export const getBom = function(projectId, state) {
    return bom.getBom(projectId, state.bom);
};

export const getDrawingPdfUrl = function(state) {
    return uiFlags.getDrawingPdfUrl(state.uiFlags);
};

export const drawingProgressShowing = function(state) {
    return uiFlags.drawingProgressShowing(state.uiFlags);
};
