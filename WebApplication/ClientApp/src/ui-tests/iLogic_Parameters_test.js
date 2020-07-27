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

/* eslint-disable no-undef */

const assert = require('assert');

const parametersElement = '.parameters';
const elements = '//div[@class="parameter" or @class="parameter checkbox"]';
const iLogicParameterList = ['LENGTH', 'WIDTH', 'HEIGHT', 'CHUTE', 'LEGS (MIN:12 | MAX:8 | STEP:2)', 'ROLLERS (MIN:3 | MAX:11 | STEP:2)'];

// compare two Arrays and return true or false
function compareArrays(array1, array2)
{
    if (array1.length != array2.length)
    {
        return false;
    }

    // compare if All iLogic parameters are the same as Model Tab has
    for (let index = 0; index < array1.length; ++index)
    {
        if(array2.indexOf(array1[index], 0) === -1)
            return false;
    }

    return true;
}

Before((I) => {
    I.amOnPage('/');
});

Feature('iLogic Parameters');

// validate that all parameters in iLogic form is displayed in the List of Parameters
Scenario('should check parameters in iLogic Form with list of parameters in Model Tab', async (I) => {

    // select Conveyor project in the Project Switcher
    I.selectProject('Conveyor');
    I.waitForElement(parametersElement, 10);

    // get list of paramater from Model tab
    const modelTabParamList = await I.grabTextFrom(elements);

    // comapre lists and validate
    const result = compareArrays(iLogicParameterList, modelTabParamList);
    assert.equal(result, true);
});