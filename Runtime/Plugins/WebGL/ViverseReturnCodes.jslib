var ViverseReturnCodesLib = {
    // Declare the symbol that will be populated by the jspre file
    $ViverseReturnCodes: {},
};

// Add the dependency and merge the library
autoAddDeps(ViverseReturnCodesLib, '$ViverseReturnCodes');
mergeInto(LibraryManager.library, ViverseReturnCodesLib);