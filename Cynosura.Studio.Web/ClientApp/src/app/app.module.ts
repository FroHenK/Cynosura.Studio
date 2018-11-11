import { BrowserModule } from "@angular/platform-browser";
import { NgModule } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { HttpClientModule } from "@angular/common/http";
import { RouterModule, Route } from "@angular/router";

import { ModalModule } from "ngx-modialog";
import { BootstrapModalModule } from "ngx-modialog/plugins/bootstrap";

import { AuthModule } from "./auth/auth.module";
import { CoreModule } from "./core/core.module";
// ADD MODULES HERE
import { EnumValueModule } from "./enumValue/enumValue.module";
import { EnumModule } from "./enum/enum.module";
import { RoleModule } from "./role/role.module";
import { UserModule } from "./user/user.module";
import { SolutionModule } from "./solution/solution.module";
import { EntityModule } from "./entity/entity.module";

import { AppComponent } from "./app.component";
import { NavMenuComponent } from "./nav-menu/nav-menu.component";
import { HomeComponent } from "./home/home.component";

var routes: Route[] = [
    { path: "", component: HomeComponent, pathMatch: "full" }
];

// ADD ROUTES HERE
routes.push({ path: "enumValue", loadChildren: () => EnumValueModule });
routes.push({ path: "enum", loadChildren: () => EnumModule });
routes.push({ path: "user", loadChildren: () => UserModule });
routes.push({ path: "role", loadChildren: () => RoleModule });
routes.push({ path: "entity", loadChildren: () => EntityModule });
routes.push({ path: "solution", loadChildren: () => SolutionModule });

@NgModule({
    declarations: [
        AppComponent,
        NavMenuComponent,
        HomeComponent
    ],
    imports: [
        BrowserModule.withServerTransition({ appId: "ng-cli-universal" }),
        HttpClientModule,
        FormsModule,
        RouterModule.forRoot(routes),
        ModalModule.forRoot(),
        BootstrapModalModule,
        AuthModule,
        CoreModule
    ],
    providers: [],
    bootstrap: [AppComponent]
})
export class AppModule { }
