angular.module('angularTests', [
    'angularTests.controllers',
    'angularTests.services',
    'angularTests.directives',
    'angularTests.filters',
    'angularTests.factories',
]).
config(['$compileProvider', function ($compileProvider) {
    $compileProvider.aHrefSanitizationWhitelist(/^\s*(https?|steam):/);
}]);

angular.module('angularTests.controllers', []);
angular.module('angularTests.services', []);
angular.module('angularTests.directives', []);
angular.module('angularTests.filters', []);
angular.module('angularTests.factories', []);

angular.module('angularTests.controllers').controller('ApplicationsController', ['$window', function ($window) {
    var ctrlr = this;
    this.applications = [];

    this.getApplications = function() {
        this.applications = JSON.parse($window.applications.getApplicationsList());

        console.log(this.applications);
    }

    this.getApplications();
}]);