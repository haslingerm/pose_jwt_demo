import {
  ChangeDetectionStrategy,
  Component,
} from "@angular/core";
import { HomeTimeDisplay } from "./home-time-display/home-time-display";

@Component({
    selector: 'app-home',
    imports: [
        HomeTimeDisplay
    ],
    templateUrl: './home.html',
    styleUrl: './home.scss'
})
export class Home {

}
