import { Component, OnInit } from "@angular/core";
import { Router, ActivatedRoute, Params } from "@angular/router";

import { Enum } from "../enum-core/enum.model";
import { EnumFilter } from "../enum-core/enum-filter.model";
import { EnumService } from "../enum-core/enum.service";

import { ModalHelper } from "../core/modal.helper";
import { StoreService } from "../core/store.service";
import { Error } from "../core/error.model";
import { Page } from "../core/page.model";

@Component({
    selector: "app-enum-list",
    templateUrl: "./enum-list.component.html"
})
export class EnumListComponent implements OnInit {
    content: Page<Enum>;
    error: Error;
    pageSize = 10;
    filter = new EnumFilter();
    private innerPageIndex: number;
    get pageIndex(): number {
        if (!this.innerPageIndex) {
            this.innerPageIndex = this.storeService.get("enumsPageIndex", 0);
        }
        return this.innerPageIndex;
    }
    set pageIndex(value: number) {
        this.innerPageIndex = value;
        this.storeService.set("enumsPageIndex", value);
    }
    private innerSolutionId: number;
    get solutionId(): number {
        if (!this.innerSolutionId) {
            this.innerSolutionId = this.storeService.get("enumsSolutionId", 0);
        }
        return this.innerSolutionId;
    }
    set solutionId(val: number) {
        this.innerSolutionId = val;
        this.storeService.set("enumsSolutionId", this.innerSolutionId);
        this.getEnums();
    }

    constructor(
        private modalHelper: ModalHelper,
        private enumService: EnumService,
        private router: Router,
        private route: ActivatedRoute,
        private storeService: StoreService
        ) {}

    ngOnInit(): void {
        this.getEnums();
    }

    getEnums(): void {
        if (this.solutionId) {
            this.enumService.getEnums({ solutionId: this.solutionId, pageIndex: this.pageIndex, pageSize: this.pageSize,
                filter: this.filter })
                .then(content => {
                    this.content = content;
                })
                .catch(error => this.error = error);
        } else {
            this.content = null;
        }
    }

    reset(): void {
        this.filter.text = null;
        this.getEnums();
    }

    edit(id: string): void {
        this.router.navigate([id], { relativeTo: this.route, queryParams: { solutionId: this.solutionId } });
    }

    add(): void {
        this.router.navigate([0], { relativeTo: this.route, queryParams: { solutionId: this.solutionId } });
    }

    delete(id: string): void {
        this.modalHelper.confirmDelete()
            .then(() => {
                this.enumService.deleteEnum({ solutionId: this.solutionId, id })
                    .then(() => {
                        this.getEnums();
                    })
                    .catch(error => this.error = error);
            })
            .catch(() => { });
    }

    onPageSelected(pageIndex: number) {
        this.pageIndex = pageIndex;
        this.getEnums();
    }
}
